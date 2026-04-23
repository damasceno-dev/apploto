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
    IOperatorsRepository operatorsRepository,
    IClientsRepository clientsRepository,
    IUnitOfWork unitOfWork,
    MemberAccountScopeGuard memberAccountScopeGuard,
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

        var callerOperator = await operatorsRepository
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId);

        if (branchUser.Role == Role.Member && callerOperator is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_LINKED_OPERATOR);
        }

        await memberAccountScopeGuard.EnsureMemberCanActOnAccount(
            branchUser.Role,
            callerOperator?.Id,
            transaction.AccountId);

        var isSameDay = transaction.Date.Date == DateTime.UtcNow.Date;
        var isOwnOperator = callerOperator is not null && callerOperator.Id == transaction.RecordedByOperatorId;
        if (isSameDay is false || isOwnOperator is false)
        {
            if (branchUser.Role is not Role.Manager and not Role.Admin)
            {
                if (isOwnOperator is false)
                {
                    throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_LINKED_OPERATOR);
                }

                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
            }
        }

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

        if (request.DueDate < transaction.Date)
        {
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE]);
        }

        if (request.PaidAt is { } paidAt && paidAt < transaction.Date)
        {
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_PAID_AT_BEFORE_DATE]);
        }

        transaction.Description = request.Description;
        transaction.DueDate = request.DueDate;
        transaction.PaidAt = request.PaidAt;
        transaction.ClientId = request.ClientId;
        transaction.TransactionTime = request.TransactionTime;

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
