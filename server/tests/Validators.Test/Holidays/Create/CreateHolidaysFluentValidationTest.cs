using CommonTestUtilities.Requests;
using server.Application.UseCases.Holidays.Create;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Holidays.Create;

public class CreateHolidaysFluentValidationTest
{
    private readonly CreateHolidaysFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenBatchHasOneValidRow()
    {
        var request = new RequestCreateHolidaysJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDescriptionIsNull()
    {
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(new DateTime(2025, 9, 7), null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenBatchHasMultipleDistinctRows()
    {
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithHolidays([
                new RequestCreateHolidayJson { Date = new DateTime(2025, 9, 7), Description = "Independência" },
                new RequestCreateHolidayJson { Date = new DateTime(2025, 11, 2), Description = "Finados" },
                new RequestCreateHolidayJson { Date = new DateTime(2025, 11, 15), Description = "Proclamação" }
            ])
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenBatchIsEmpty()
    {
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithEmptyList()
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.HOLIDAY_CREATE_BATCH_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenRowDateIsDefault()
    {
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.HOLIDAY_DATE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionExceedsMaxLength()
    {
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(new DateTime(2025, 9, 7), new string('A', CreateHolidaysFluentValidation.MaximumDescriptionLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.HOLIDAY_DESCRIPTION_MAX_LENGTH,
                CreateHolidaysFluentValidation.MaximumDescriptionLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenBatchHasDuplicateDates()
    {
        var duplicateDate = new DateTime(2025, 9, 7);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithHolidays([
                new RequestCreateHolidayJson { Date = duplicateDate, Description = "First" },
                new RequestCreateHolidayJson { Date = duplicateDate, Description = "Second" }
            ])
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
    }
}
