using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Create;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.SafeDeposit;

/// <summary>
/// <c>POST /transaction/safe-deposit</c> — <i>Depósito Dinheiro</i> (M9.6 Phase 9, §6.19 O09). One operation, two
/// protected legs in one commit: the explicit cashbox <c>Out</c> on the cashbox's session (the system deposit type)
/// and the generated cofre <c>In</c>, linked by <see cref="CustodyTransfer"/>. The cofre has no session, count or
/// variance. Both accounts are locked in ascending order under the shared month boundary; the destination is
/// re-read inside the scope, so a cofre deactivated or a grant revoked meanwhile refuses the whole operation. The
/// leg pair finalizes and cancels through the source like a fiado pair. The caller's current authority over both
/// accounts is checked before a stored outcome is replayed.
/// </summary>
public class SafeDepositUseCase(
    IAuthenticationService authenticationService,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    TransactionCreatePreamble transactionCreatePreamble,
    ITransactionsRepository transactionsRepository,
    ITransactionTypesRepository transactionTypesRepository,
    IAccountsRepository accountsRepository,
    ICustodyTransfersRepository custodyTransfersRepository,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    FinancialCommandIdempotency financialCommandIdempotency,
    IBranchClock branchClock,
    LockDateGuard lockDateGuard,
    SessionRevisionService sessionRevisions,
    IUnitOfWork unitOfWork)
{
    public const string IdempotencyEndpoint = "POST /transaction/safe-deposit";

    public async Task<ResponseCreateTransactionJson> Execute(
        RequestSafeDepositJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        Validate(request);
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var utcNow = branchClock.UtcNow();

        // Current authority first: a Member who lost either grant never receives a stored outcome.
        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        EnsureScope(branchUser, memberScope, request);

        var replay = await financialCommandIdempotency
            .TryReplay<RequestSafeDepositJson, ResponseCreateTransactionJson>(
                idempotencyKey,
                IdempotencyEndpoint,
                branchUser.BranchId,
                branchUser.UserId,
                request,
                utcNow,
                ct);
        if (replay is not null)
            return replay;

        await using var coordination = await dailyCloseAccountCoordination.AcquireMany(
            branchUser.BranchId,
            [request.AccountId, request.SafeAccountId],
            ct);

        var idempotency = await financialCommandIdempotency
            .Prepare<RequestSafeDepositJson, ResponseCreateTransactionJson>(
                idempotencyKey!,
                IdempotencyEndpoint,
                branchUser.BranchId,
                branchUser.UserId,
                request,
                utcNow,
                ct);
        if (idempotency.IsReplay)
        {
            await coordination.Complete(ct);
            return idempotency.ReplayResponse!;
        }

        var depositType = await transactionTypesRepository.GetActiveSystemTypeByBranchIdAsNoTracking(
            branchUser.BranchId, TransactionTypeSystemRole.SafeDepositOut, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.BRANCH_FINANCIAL_SEEDS_MISSING);
        var safeInType = await transactionTypesRepository.GetActiveSystemTypeByBranchIdAsNoTracking(
            branchUser.BranchId, TransactionTypeSystemRole.SafeDepositIn, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.BRANCH_FINANCIAL_SEEDS_MISSING);

        var createRequest = request.ToCreateRequest(depositType.Id);
        var context = await transactionCreatePreamble.ResolveSafeDeposit(createRequest, ct);

        // The destination is read inside the scope: active, this branch, a cofre, and in a Member's own scope.
        var safe = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.SafeAccountId, branchUser.BranchId, ct);
        if (safe is null || safe.Type != AccountType.Safe)
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_SAFE_NOT_FOUND);
        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, context.MemberScope!, safe.Id);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            request.Date.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED,
            ct);

        var sessionContext = new SessionRowContext(
            context.BranchUser,
            context.MemberScope!,
            request.SessionId,
            request.HistoricalReason,
            request.OpeningAcknowledgment);
        var ownSession = await dailyCloseLedgerGuard.ResolveSessionForNewRow(
            request.AccountId,
            context.Account!.Type,
            request.Date.Date,
            sessionContext,
            ct);

        var source = createRequest.ToTransaction(
            transactionType: context.TransactionType,
            recordedByOperatorId: context.RecordedByOperatorId,
            dueDate: context.DueDate!.Value,
            createdByUserId: context.BranchUser.UserId,
            branchId: context.BranchUser.BranchId);
        source.SessionId = ownSession.SessionId;

        var destination = new Transaction
        {
            Date = source.Date,
            Value = source.Value,
            TransactionTime = source.TransactionTime,
            Description = source.Description,
            TransactionTypeId = safeInType.Id,
            CategoryId = safeInType.CategoryId,
            Direction = safeInType.Category.DefaultDirection,
            AccountId = safe.Id,
            DueDate = source.Date,
            RecordedByOperatorId = source.RecordedByOperatorId,
            CreatedByUserId = source.CreatedByUserId,
            Status = source.Status,
            BranchId = source.BranchId,
            GenerationKind = TransactionGenerationKind.SafeCashDeposit,
            GeneratedFromTransactionId = source.Id
        };

        var transfer = new CustodyTransfer
        {
            BranchId = branchUser.BranchId,
            AssetKind = CustodyTransferAssetKind.Cash,
            Value = source.Value,
            SourceAccountId = source.AccountId,
            SourceSessionId = source.SessionId,
            SourceTransactionId = source.Id,
            DestinationAccountId = safe.Id,
            DestinationTransactionId = destination.Id,
            CreatedByUserId = branchUser.UserId
        };

        await transactionsRepository.Add(source, ct);
        await transactionsRepository.Add(destination, ct);
        await custodyTransfersRepository.Add(transfer, ct);
        await sessionRevisions.RecordLedgerChange(
            branchUser.BranchId,
            new[] { source, destination }.Where(row => row.Status == TransactionStatus.Active),
            ct);
        await unitOfWork.Commit(ct);

        var response = source.ToCreateResponse();
        response.GeneratedTransactionId = destination.Id;
        response.CustodyTransferId = transfer.Id;
        TransactionSessionBinding.Apply(response, ownSession);
        FinancialCommandIdempotency.Complete(idempotency, source.Id, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return response;
    }

    private void EnsureScope(BranchUser branchUser, MemberAccountScope scope, RequestSafeDepositJson request)
    {
        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, scope, request.AccountId);
        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, scope, request.SafeAccountId);
    }

    private static void Validate(RequestSafeDepositJson request)
    {
        var result = new SafeDepositFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}

internal static class SafeDepositMapper
{
    public static RequestCreateTransactionJson ToCreateRequest(this RequestSafeDepositJson request, Guid depositTypeId)
    {
        return new RequestCreateTransactionJson
        {
            Date = request.Date,
            Value = request.Value,
            TransactionTime = request.TransactionTime,
            Description = request.Description,
            TransactionTypeId = depositTypeId,
            AccountId = request.AccountId,
            RecordedByOperatorId = request.RecordedByOperatorId,
            SaveAsDraft = request.SaveAsDraft,
            SessionId = request.SessionId,
            HistoricalReason = request.HistoricalReason,
            OpeningAcknowledgment = request.OpeningAcknowledgment
        };
    }
}
