using server.Application.Services.Members;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Finalize;

public class FinalizeTransactionUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    ITransactionMutationPermissionGuard transactionMutationPermissionGuard,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    FiadoPhysicalPairService fiadoPhysicalPairService,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTransactionJson> Execute(
        Guid transactionId,
        uint expectedVersion,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var transactionKey = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(
            transactionId,
            branchUser.BranchId,
            ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        // A generated Terminal leg never moves on its own (M7.7 Phase 5.7); its source carries it.
        if (transactionKey.GenerationKind is not null)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_GENERATED_ROW_READ_ONLY);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);

        var counterpartKey = await transactionsRepository.GetGeneratedBySourceIdAndBranchIdAsNoTracking(
            transactionKey.Id,
            branchUser.BranchId,
            ct);

        await using var coordination = counterpartKey is null
            ? await dailyCloseAccountCoordination.Acquire(branchUser.BranchId, transactionKey.AccountId, ct)
            : await dailyCloseAccountCoordination.AcquireMany(
                branchUser.BranchId,
                [transactionKey.AccountId, counterpartKey.AccountId],
                ct);

        var transaction = await transactionsRepository.GetByIdAndBranchId(transactionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        if (transaction.Status != TransactionStatus.Draft)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_CANNOT_FINALIZE_NON_DRAFT);
        }

        // A Draft installment of a cheque plan finalizes only through the plan command (M7.7 7.6.2), so no
        // single-row path can activate part of a plan. A standalone Draft keeps this individual finalize.
        if (transaction.OriginTransactionId is not null)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_PLAN_FINALIZE_REQUIRED);

        if (transaction.Version != expectedVersion)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_STALE_WRITE);

        if (branchUser.Role == Role.Member && memberScope.LinkedOperator is not null)
        {
            memberAccountScopeGuard.EnsureMemberCanActOnAccount(
                branchUser.Role,
                memberScope,
                transaction.AccountId);
        }

        var utcNow = branchClock.UtcNow();
        transactionMutationPermissionGuard.EnsureAllowed(
            transaction,
            branchUser.Role,
            memberScope.LinkedOperator,
            utcNow);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            transaction.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED,
            ct);

        await dailyCloseLedgerGuard.EnsureLedgerIsMutable(
            branchUser.BranchId,
            transaction.AccountId,
            transaction.Date.Date,
            ct);

        // The physical leg (M7.7 Phase 5.6) activates in the same commit, under its own terminal's ledger guard.
        var counterpart = counterpartKey is null
            ? null
            : await transactionsRepository.GetGeneratedBySourceIdAndBranchId(transaction.Id, branchUser.BranchId, ct);
        if (counterpart is not null)
            await fiadoPhysicalPairService.EnsureCounterpartMutable(counterpart, branchUser.Role, memberScope, ct);

        transaction.Status = TransactionStatus.Active;
        transaction.UpdatedAt = utcNow;
        transaction.UpdatedByUserId = branchUser.UserId;

        if (counterpart is not null)
        {
            counterpart.Status = TransactionStatus.Active;
            counterpart.UpdatedAt = utcNow;
            counterpart.UpdatedByUserId = branchUser.UserId;
        }

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return transaction.ToTransactionResponse(counterpart?.Id);
    }
}
