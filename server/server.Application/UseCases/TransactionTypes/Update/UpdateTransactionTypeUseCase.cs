using server.Application.UseCases.TransactionTypes;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes.Update;

/// <summary>
/// Updates the mutable metadata of a TransactionType (Name, SettlementRule, RequiresTabAccountAndClient).
/// Changes are forward-only: existing <c>Transaction</c> rows are NOT recomputed.
/// Each Transaction captured <c>Direction</c>, <c>CategoryId</c>, and <c>DueDate</c> as
/// immutable denormalized snapshots at create time per §6.1.
/// </summary>
public class UpdateTransactionTypeUseCase(
    IAuthenticationService authenticationService,
    ITransactionTypesRepository transactionTypesRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTransactionTypeJson> Execute(Guid transactionTypeId, RequestUpdateTransactionTypeJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var transactionType = await transactionTypesRepository.GetActiveByIdWithCategoryAndBranchId(
            transactionTypeId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);

        var exists = await transactionTypesRepository.ExistsActiveByCategoryIdAndName(
            transactionType.CategoryId, request.Name.Trim(), exceptId: transactionTypeId);
        if (exists)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT);

        transactionType.Name = request.Name.Trim();
        transactionType.SettlementRule = request.SettlementRule;
        transactionType.RequiresTabAccountAndClient = request.RequiresTabAccountAndClient;

        await unitOfWork.Commit();

        return transactionType.ToResponse();
    }

    private static void Validate(RequestUpdateTransactionTypeJson request)
    {
        var result = new UpdateTransactionTypeFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
