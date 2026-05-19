using CommonTestUtilities.Requests;
using server.Application.UseCases.Products;
using server.Application.UseCases.Products.Update;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Products.Update;

public class UpdateProductFluentValidationTest
{
    private readonly UpdateProductFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUpdateProductJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDisplayOrderIsZero()
    {
        var request = new RequestUpdateProductJsonBuilder().WithDisplayOrder(0).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestUpdateProductJsonBuilder().WithName(string.Empty).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.PRODUCT_NAME_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var name = new string('a', ProductValidationExtensions.ProductNameMaxLength + 1);
        var request = new RequestUpdateProductJsonBuilder().WithName(name).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.PRODUCT_NAME_MAX_LENGTH, ProductValidationExtensions.ProductNameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDisplayOrderIsNegative()
    {
        var request = new RequestUpdateProductJsonBuilder().WithDisplayOrder(-1).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.PRODUCT_DISPLAY_ORDER_INVALID);
    }
}
