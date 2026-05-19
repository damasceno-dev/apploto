using server.Application.UseCases.Categories;
using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Categories.List;

public class ListCategoriesUseCase(
    IAuthenticationService authenticationService,
    ICategoriesRepository categoriesRepository)
{
    public async Task<ResponseListCategoriesJson> Execute()
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var categories = await categoriesRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
        return categories.ToListResponse();
    }
}
