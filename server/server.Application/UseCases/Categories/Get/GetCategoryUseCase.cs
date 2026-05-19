using server.Application.UseCases.Categories;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Categories.Get;

public class GetCategoryUseCase(
    IAuthenticationService authenticationService,
    ICategoriesRepository categoriesRepository)
{
    public async Task<ResponseCategoryJson> Execute(Guid categoryId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var category = await categoriesRepository.GetActiveByIdAndBranchIdAsNoTracking(categoryId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.CATEGORY_NOT_FOUND);

        return category.ToResponse();
    }
}
