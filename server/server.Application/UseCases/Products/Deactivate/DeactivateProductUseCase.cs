using server.Application.Services.DailyCloses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Products.Deactivate;

public class DeactivateProductUseCase(
    IAuthenticationService authenticationService,
    IProductsRepository productsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    IUnitOfWork unitOfWork)
{
    public async Task Execute(Guid productId)
    {
        if (productId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.PRODUCT_ID_EMPTY]);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var product = await productsRepository.GetActiveByIdAndBranchId(productId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.PRODUCT_NOT_FOUND);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId);
        if (product.Id == cashVarianceProductId)
            throw new ConflictException(ResourcesErrorMessages.PRODUCT_SYSTEM_PROTECTED);

        product.Active = false;

        await unitOfWork.Commit();
    }
}
