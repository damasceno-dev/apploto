using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.CreateInstallment;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.InstallmentPreview;

/// <summary>
/// Non-persisting twin of <c>CreateTransactionInstallmentUseCase</c>. Keeps the row-plan
/// preview and adds the downstream impact forecast (open-cheque aging, fiado balance, cash variance)
/// for the would-be cheque plan. Same auth/scope as <c>POST /transaction/installment</c>:
/// <c>[TokenAuthenticateBranch]</c> with the Member linked-operator + account-scope checks inherited
/// from <see cref="TransactionCreatePreamble"/>, so anyone who can create the plan can preview it
/// (preview/write parity).
/// <para>
/// Injects neither <c>ITransactionsRepository</c> nor <c>IUnitOfWork</c>: a preview cannot commit by
/// construction (pinned by the Phase 12.3 scan over <c>UseCases/Transactions/InstallmentPreview/</c>).
/// Every impact section stays scoped to the previewed <c>(account, client, date)</c> — the projector
/// only sees the preamble-resolved context, never a branch-wide balance, receivable, or all-account
/// variance summary.
/// </para>
/// </summary>
public class PreviewInstallmentPlanUseCase(
    TransactionCreatePreamble preamble,
    InstallmentPlanBuilder installmentPlanBuilder,
    TransactionEditImpactProjector installmentImpactProjector,
    IBranchClock branchClock)
{
    public async Task<ResponseInstallmentPreviewJson> Execute(
        RequestCreateTransactionInstallmentJson request,
        DateTime? asOfDate = null,
        CancellationToken ct = default)
    {
        Validate(request);
        
        var ctx = await preamble.Resolve(request);

        if (ctx.TransactionType.SettlementRule != SettlementRule.OperatorEnteredCheque)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);

        var installmentPlan = installmentPlanBuilder.Build(request, ctx.BranchUser.BranchId, Guid.NewGuid(), ctx.BranchHolidays);

        var resolvedAsOfDate = asOfDate ?? branchClock.LocalBusinessDate(branchClock.UtcNow());
        var hypotheticalPlan = new HypotheticalTransactionInstallmentPlan(request, ctx, installmentPlan);
        var impact = await installmentImpactProjector.ProjectInstallment(hypotheticalPlan, resolvedAsOfDate, ct);

        return new ResponseInstallmentPreviewJson
        {
            TotalValue = installmentPlan.Sum(r => r.Value),
            InstallmentCount = installmentPlan.Count,
            Rows = installmentPlan.Select((row, i) => new ResponseInstallmentPreviewRowJson
            {
                Index = i + 1,
                DueDate = row.DueDate,
                Value = row.Value,
                Description = row.Description
            }).ToList(),
            Impact = impact
        };
    }

    private static void Validate(RequestCreateTransactionInstallmentJson request)
    {
        var result = new CreateTransactionInstallmentFluentValidation().Validate(request);
        if (!result.IsValid)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
