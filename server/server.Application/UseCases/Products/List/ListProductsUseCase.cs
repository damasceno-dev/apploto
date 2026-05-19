using server.Application.UseCases.Products;
using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Products.List;

public class ListProductsUseCase(
    IAuthenticationService authenticationService,
    IProductsRepository productsRepository)
{
    public async Task<ResponseListProductsJson> Execute()
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var products = await productsRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
        return products.ToListResponse();
    }
}
