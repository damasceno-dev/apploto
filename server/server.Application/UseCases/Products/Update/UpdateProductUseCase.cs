using server.Application.Services.DailyCloses;
using server.Application.UseCases.Products;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Products.Update;

public class UpdateProductUseCase(
    IAuthenticationService authenticationService,
    IProductsRepository productsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseProductJson> Execute(Guid productId, RequestUpdateProductJson request)
    {
        if (productId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.PRODUCT_ID_EMPTY]);

        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var product = await productsRepository.GetActiveByIdAndBranchId(productId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.PRODUCT_NOT_FOUND);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId);
        VerifyCashVarianceProductName(productId, cashVarianceProductId, request.Name.Trim());

        var exists = await productsRepository.ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), exceptId: productId);
        if (exists)
            throw new ConflictException(ResourcesErrorMessages.PRODUCT_NAME_CONFLICT);

        product.Name = request.Name.Trim();
        product.DisplayOrder = request.DisplayOrder;

        await unitOfWork.Commit();

        return product.ToResponse();
    }

    /// <summary>
    /// XOR Logic to protect CashVarianceProductName
    /// Renaming "Diferença Caixa" to "Soda": (True != False) is True → Throws (Correct, renaming is blocked)
    /// Renaming "Soda" to "Diferença Caixa": (False != True) is True → Throws (Correct, name is reserved)
    /// Updating Order of "Diferença Caixa": (True != True) is False → Allowed (Correct, order changes are allowed)
    /// Updating Normal Product normally: (False != False) is False → Allowed (Correct)
    /// </summary>
    /// <param name="productId"></param>
    /// <param name="cashVarianceProductId"></param>
    /// <param name="requestName"></param>
    /// <exception cref="OnValidationException"></exception>
    private static void VerifyCashVarianceProductName(Guid productId, Guid cashVarianceProductId, string requestName)
    {
        var isCashVarianceProduct = productId == cashVarianceProductId;
        var isReservedName = requestName.Equals(CashVarianceProductResolver.CashVarianceProductName, StringComparison.Ordinal);

        // XOR: throw if the system product is being renamed or a normal product is taking the reserved name.
        if (isCashVarianceProduct != isReservedName)
            throw new OnValidationException([ResourcesErrorMessages.PRODUCT_SYSTEM_PROTECTED]);
    }

    private static void Validate(RequestUpdateProductJson request)
    {
        var result = new UpdateProductFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
