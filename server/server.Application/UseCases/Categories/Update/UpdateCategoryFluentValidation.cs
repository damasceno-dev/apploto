using FluentValidation;
using server.Application.UseCases.Categories;
using server.Communication.Requests;

namespace server.Application.UseCases.Categories.Update;

public class UpdateCategoryFluentValidation : AbstractValidator<RequestUpdateCategoryJson>
{
    public UpdateCategoryFluentValidation()
    {
        RuleFor(r => r.Name).ValidateCategoryName();
    }
}
