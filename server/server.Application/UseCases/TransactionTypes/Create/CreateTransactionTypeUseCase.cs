using server.Application.UseCases.TransactionTypes;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes.Create;

public class CreateTransactionTypeUseCase(
    IAuthenticationService authenticationService,
    ICategoriesRepository categoriesRepository,
    ITransactionTypesRepository transactionTypesRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseTransactionTypeJson> Execute(RequestCreateTransactionTypeJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var category = await categoriesRepository.GetActiveByIdAndBranchIdAsNoTracking(request.CategoryId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.CATEGORY_NOT_FOUND);

        var exists = await transactionTypesRepository.ExistsActiveByCategoryIdAndName(category.Id, request.Name.Trim());
        if (exists)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT);

        var transactionType = request.ToDomain();
        await transactionTypesRepository.Add(transactionType);
        await unitOfWork.Commit();

        var created = await transactionTypesRepository.GetActiveByIdWithCategoryAndBranchIdAsNoTracking(
            transactionType.Id, branchUser.BranchId);

        return created!.ToResponse();
    }

    private static void Validate(RequestCreateTransactionTypeJson request)
    {
        var result = new CreateTransactionTypeFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
