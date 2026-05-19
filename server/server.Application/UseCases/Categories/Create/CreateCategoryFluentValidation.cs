using FluentValidation;
using server.Application.UseCases.Categories;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.Categories.Create;

public class CreateCategoryFluentValidation : AbstractValidator<RequestCreateCategoryJson>
{
    public CreateCategoryFluentValidation()
    {
        RuleFor(r => r.Name).ValidateCategoryName();
        RuleFor(r => r.DefaultDirection)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.CATEGORY_DEFAULT_DIRECTION_INVALID);
    }
}
