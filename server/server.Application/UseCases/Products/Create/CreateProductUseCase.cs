using server.Application.Services.DailyCloses;
using server.Application.UseCases.Products;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Products.Create;

public class CreateProductUseCase(
    IAuthenticationService authenticationService,
    IProductsRepository productsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseProductJson> Execute(RequestCreateProductJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        if (request.Name.Trim().Equals(CashVarianceProductResolver.CashVarianceProductName, StringComparison.Ordinal))
            throw new ConflictException(ResourcesErrorMessages.PRODUCT_NAME_CONFLICT);

        var exists = await productsRepository.ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim());
        if (exists)
            throw new ConflictException(ResourcesErrorMessages.PRODUCT_NAME_CONFLICT);

        var product = request.ToDomain(branchUser.BranchId);
        await productsRepository.Add(product);
        await unitOfWork.Commit();

        return product.ToResponse();
    }

    private static void Validate(RequestCreateProductJson request)
    {
        var result = new CreateProductFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
