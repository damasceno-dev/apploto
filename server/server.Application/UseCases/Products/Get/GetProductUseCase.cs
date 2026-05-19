using server.Application.UseCases.Products;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Products.Get;

public class GetProductUseCase(
    IAuthenticationService authenticationService,
    IProductsRepository productsRepository)
{
    public async Task<ResponseProductJson> Execute(Guid productId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var product = await productsRepository.GetActiveByIdAndBranchIdAsNoTracking(productId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.PRODUCT_NOT_FOUND);

        return product.ToResponse();
    }
}
