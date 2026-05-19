using server.Application.UseCases.Categories;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Categories.Update;

public class UpdateCategoryUseCase(
    IAuthenticationService authenticationService,
    ICategoriesRepository categoriesRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCategoryJson> Execute(Guid categoryId, RequestUpdateCategoryJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var category = await categoriesRepository.GetActiveByIdAndBranchId(categoryId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.CATEGORY_NOT_FOUND);

        var exists = await categoriesRepository.ExistsActiveByBranchIdAndName(
            branchUser.BranchId, request.Name.Trim(), exceptCategoryId: categoryId);
        if (exists)
            throw new ConflictException(ResourcesErrorMessages.CATEGORY_NAME_CONFLICT);

        category.Name = request.Name.Trim();

        await unitOfWork.Commit();

        return category.ToResponse();
    }

    private static void Validate(RequestUpdateCategoryJson request)
    {
        var result = new UpdateCategoryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
