using server.Application.Services.Members;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Cancel;

public class CancelTransactionUseCase(
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
    public async Task<ResponseTransactionJson> Execute(
        Guid transactionId,
        RequestCancelTransactionJson request,
        uint expectedVersion,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

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

        if (transaction.Status == TransactionStatus.Cancelled)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_ALREADY_CANCELLED);

        if (transaction.Version != expectedVersion)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_STALE_WRITE);

        if (branchUser.Role == Role.Member && memberScope.LinkedOperator is not null)
        {
            memberAccountScopeGuard.EnsureMemberCanActOnAccount(
                branchUser.Role,
                memberScope,
                transaction.AccountId);
        }

        // Capture the clock instant ONCE so cancellation audit and generic update
        // audit fields are stamped from the same timestamp. The same instant is also
        // passed to the mutation permission guard so its same-day comparison and the
        // persisted UpdatedAt cannot drift apart under concurrent clock ticks.
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

        // Cancellation never touches installment siblings: the loaded row is the only
        // entity mutated. The installment-sibling-isolation Web API test in 7.9 is the
        // executable contract for this guarantee.
        transaction.Status = TransactionStatus.Cancelled;
        transaction.CancelledAt = utcNow;
        transaction.CancelledByUserId = branchUser.UserId;
        transaction.CancellationReason = request.CancellationReason.Trim();
        transaction.UpdatedAt = utcNow;
        transaction.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return transaction.ToTransactionResponse();
    }

    private static void Validate(RequestCancelTransactionJson request)
    {
        var result = new CancelTransactionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
