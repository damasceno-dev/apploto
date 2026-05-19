using FluentValidation;
using server.Application.UseCases.Products;
using server.Communication.Requests;

namespace server.Application.UseCases.Products.Create;

public class CreateProductFluentValidation : AbstractValidator<RequestCreateProductJson>
{
    public CreateProductFluentValidation()
    {
        RuleFor(r => r.Name).ValidateProductName();
        RuleFor(r => r.DisplayOrder).ValidateProductDisplayOrder();
    }
}
