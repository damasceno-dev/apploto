using server.Application.UseCases.Categories;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Categories.Create;

public class CreateCategoryUseCase(
    IAuthenticationService authenticationService,
    ICategoriesRepository categoriesRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCategoryJson> Execute(RequestCreateCategoryJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var exists = await categoriesRepository.ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim());
        if (exists)
            throw new ConflictException(ResourcesErrorMessages.CATEGORY_NAME_CONFLICT);

        var category = request.ToDomain(branchUser.BranchId);
        await categoriesRepository.Add(category);
        await unitOfWork.Commit();

        return category.ToResponse();
    }

    private static void Validate(RequestCreateCategoryJson request)
    {
        var result = new CreateCategoryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
