using server.Application.Services.DailyCloses;
using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Create;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.CreatePreview;

/// <summary>
/// Non-persisting twin of <see cref="CreateTransactionUseCase"/>. Answers "if I record this new
/// transaction, what moves?" by mirroring the write chain step-for-step — validate → resolve the
/// shared <see cref="TransactionCreatePreamble"/> context — minus the <c>Add</c>/<c>Commit</c>, then
/// projects the create's impact. Same permission as <c>POST /transaction</c>:
/// <see cref="server.Filters"/> applies <c>[TokenAuthenticateBranch]</c> and the preamble performs
/// the same Member linked-operator + account-scope enforcement, so anyone who can create the row can
/// preview it (preview/write parity) — there is no Manager/Admin gate.
/// <para>
/// Injects no <see cref="IUnitOfWork"/> or <see cref="ITransactionsRepository"/>: a preview cannot
/// commit by construction (pinned at the architecture level by the Phase 12.3 scan). For Member
/// callers the projector only ever sees the preamble-resolved <c>(account, client, date)</c>, never a
/// branch-wide balance, branch-wide receivable, or all-account variance summary.
/// </para>
/// </summary>
public class PreviewCreateTransactionUseCase(
    IAuthenticationService authenticationService,
    TransactionCreatePreamble transactionCreatePreamble,
    TransactionEditImpactProjector createImpactProjector,
    FiadoPhysicalPairService fiadoPhysicalPairService,
    IBranchClock branchClock)
{
    public async Task<ResponseCreateTransactionPreviewJson> Execute(
        RequestCreateTransactionJson request,
        DateTime? asOfDate = null,
        CancellationToken ct = default)
    {
        // [TokenAuthenticateBranch] admits any branch role; the preamble below applies the same
        // Member linked-operator + account-scope enforcement the write twin uses, so there is no
        // separate Manager/Admin role check here.
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        Validate(request);

        var createContext = await transactionCreatePreamble.Resolve(request, ct);

        // A counted-stock fiado sale is previewed as the whole pair the write would record (M7.7 Phase 5.6): the
        // same pairing, Member terminal scope and terminal open-close checks, then the terminal's impact below.
        Guid? pairedTerminalId = null;
        if (request.HandsOutCountedStock)
        {
            (pairedTerminalId, _) = await fiadoPhysicalPairService.ValidateCounterpart(
                branchUser.BranchId,
                request.AccountId,
                request.Date,
                new SessionRowContext(
                    createContext.BranchUser,
                    createContext.MemberScope!,
                    request.SessionId,
                    request.HistoricalReason,
                    request.OpeningAcknowledgment,
                    Preview: true),
                lockedTerminalId: null,
                ct);
        }

        var create = new HypotheticalTransactionCreate(request, createContext) { PairedTerminalAccountId = pairedTerminalId };
        var resolvedAsOfDate = asOfDate ?? branchClock.LocalBusinessDate(branchClock.UtcNow());
        var impact = await createImpactProjector.ProjectCreate(create, resolvedAsOfDate, branchUser.BranchId, ct);

        return new ResponseCreateTransactionPreviewJson
        {
            Impact = impact,
            Warnings = []
        };
    }

    private static void Validate(RequestCreateTransactionJson request)
    {
        var result = new CreateTransactionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
