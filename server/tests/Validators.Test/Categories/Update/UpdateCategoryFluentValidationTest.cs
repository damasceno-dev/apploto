using CommonTestUtilities.Requests;
using server.Application.UseCases.Categories;
using server.Application.UseCases.Categories.Update;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Categories.Update;

public class UpdateCategoryFluentValidationTest
{
    private readonly UpdateCategoryFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestUpdateCategoryJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.CATEGORY_NAME_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var name = new string('a', CategoryValidationExtensions.CategoryNameMaxLength + 1);
        var request = new RequestUpdateCategoryJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.CATEGORY_NAME_MAX_LENGTH, CategoryValidationExtensions.CategoryNameMaxLength));
    }
}
