using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Products;

internal static class ProductValidationExtensions
{
    internal const int ProductNameMaxLength = 255;

    extension<T>(IRuleBuilder<T, string> rule)
    {
        public void ValidateProductName()
        {
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.PRODUCT_NAME_REQUIRED);
            rule
                .MaximumLength(ProductNameMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.PRODUCT_NAME_MAX_LENGTH, ProductNameMaxLength));
        }
    }

    extension<T>(IRuleBuilder<T, int> rule)
    {
        public void ValidateProductDisplayOrder()
        {
            rule
                .GreaterThanOrEqualTo(0)
                .WithMessage(ResourcesErrorMessages.PRODUCT_DISPLAY_ORDER_INVALID);
        }
    }
}
