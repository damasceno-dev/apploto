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
    IMemberTransactionScopeResolver memberTransactionScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    ITransactionMutationPermissionGuard transactionMutationPermissionGuard,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTransactionJson> Execute(Guid transactionId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var transaction = await transactionsRepository.GetByIdAndBranchId(transactionId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        if (transaction.Status != TransactionStatus.Draft)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_CANNOT_FINALIZE_NON_DRAFT);
        }

        var memberScope = await memberTransactionScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

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

        await lockDateGuard.EnsureNotLocked(branchUser.BranchId, transaction.Date);

        transaction.Status = TransactionStatus.Active;
        transaction.UpdatedAt = utcNow;
        transaction.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit();

        return transaction.ToTransactionResponse();
    }
}
