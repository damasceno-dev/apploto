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
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTransactionJson> Execute(Guid transactionId, CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var transactionKey = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(
            transactionId,
            branchUser.BranchId,
            ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);

        await using var coordination = await dailyCloseAccountCoordination.Acquire(
            branchUser.BranchId,
            transactionKey.AccountId,
            ct);

        var transaction = await transactionsRepository.GetByIdAndBranchId(transactionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        if (transaction.Status != TransactionStatus.Draft)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_CANNOT_FINALIZE_NON_DRAFT);
        }

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

        transaction.Status = TransactionStatus.Active;
        transaction.UpdatedAt = utcNow;
        transaction.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return transaction.ToTransactionResponse();
    }
}
