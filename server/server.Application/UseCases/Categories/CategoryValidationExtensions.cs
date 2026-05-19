using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Categories;

internal static class CategoryValidationExtensions
{
    internal const int CategoryNameMaxLength = 255;

    extension<T>(IRuleBuilder<T, string> rule)
    {
        public void ValidateCategoryName()
        {
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.CATEGORY_NAME_REQUIRED);
            rule
                .MaximumLength(CategoryNameMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.CATEGORY_NAME_MAX_LENGTH, CategoryNameMaxLength));
        }
    }
}
