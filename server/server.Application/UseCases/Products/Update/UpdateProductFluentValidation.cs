using FluentValidation;
using server.Application.UseCases.Products;
using server.Communication.Requests;

namespace server.Application.UseCases.Products.Update;

public class UpdateProductFluentValidation : AbstractValidator<RequestUpdateProductJson>
{
    public UpdateProductFluentValidation()
    {
        RuleFor(r => r.Name).ValidateProductName();
        RuleFor(r => r.DisplayOrder).ValidateProductDisplayOrder();
    }
}
