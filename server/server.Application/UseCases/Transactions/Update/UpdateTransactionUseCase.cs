using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Update;

public class UpdateTransactionUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IClientsRepository clientsRepository,
    IUnitOfWork unitOfWork,
    IMemberTransactionScopeResolver memberTransactionScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    ITransactionMutationPermissionGuard transactionMutationPermissionGuard,
    LockDateGuard lockDateGuard)
{
    public async Task<ResponseTransactionJson> Execute(Guid transactionId, RequestUpdateTransactionJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        var transaction = await transactionsRepository.GetByIdAndBranchId(transactionId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        if (transaction.Status == TransactionStatus.Cancelled)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_CANNOT_UPDATE_CANCELLED);
        }

        var memberScope = await memberTransactionScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

        if (branchUser.Role == Role.Member && memberScope.LinkedOperator is not null)
        {
            memberAccountScopeGuard.EnsureMemberCanActOnAccount(
                branchUser.Role,
                memberScope,
                transaction.AccountId);
        }

        // Keep mutation permission first only for Members with no linked operator so
        // they surface TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK. Linked Members with
        // no active accounts are rejected by account scope before mutation rules.
        var utcNow = DateTime.UtcNow;
        transactionMutationPermissionGuard.EnsureAllowed(
            transaction,
            branchUser.Role,
            memberScope.LinkedOperator,
            utcNow);

        await lockDateGuard.EnsureNotLocked(branchUser.BranchId, transaction.Date);

        if (transaction.TransactionType.RequiresTabAccountAndClient && request.ClientId is null)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_FIADO_REQUIRES_CLIENT);
        }

        if (request.ClientId is { } newClientId)
        {
            var client = await clientsRepository.GetActiveByIdAndBranchIdAsNoTracking(newClientId, branchUser.BranchId);
            if (client is null)
            {
                throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);
            }
        }

        UpdateTransactionEntityRelativeValidator.EnsureValid(transaction, request);

        transaction.Description = request.Description;
        transaction.DueDate = request.DueDate;
        transaction.PaidAt = request.PaidAt;
        transaction.ClientId = request.ClientId;
        transaction.TransactionTime = request.TransactionTime;
        transaction.UpdatedAt = utcNow;
        transaction.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit();

        return transaction.ToTransactionResponse();
    }

    private static void Validate(RequestUpdateTransactionJson request)
    {
        var result = new UpdateTransactionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
