using CommonTestUtilities.Requests;
using server.Application.UseCases.Categories;
using server.Application.UseCases.Categories.Create;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Categories.Create;

public class CreateCategoryFluentValidationTest
{
    private readonly CreateCategoryFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDirectionIsOut()
    {
        var request = new RequestCreateCategoryJsonBuilder()
            .WithDefaultDirection(Direction.Out)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateCategoryJsonBuilder()
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
        var request = new RequestCreateCategoryJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.CATEGORY_NAME_MAX_LENGTH, CategoryValidationExtensions.CategoryNameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDefaultDirectionIsInvalid()
    {
        var request = new RequestCreateCategoryJsonBuilder()
            .WithDefaultDirection((Direction)99)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.CATEGORY_DEFAULT_DIRECTION_INVALID);
    }
}
