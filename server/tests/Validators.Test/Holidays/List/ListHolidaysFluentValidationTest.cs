using CommonTestUtilities.Requests;
using server.Application.UseCases.Holidays.List;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Holidays.List;

public class ListHolidaysFluentValidationTest
{
    private readonly ListHolidaysFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDefaultPaging()
    {
        var request = new RequestListHolidaysJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDateRangeIsValid()
    {
        var request = new RequestListHolidaysJsonBuilder()
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 12, 31))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsLessThanOne()
    {
        var request = new RequestListHolidaysJsonBuilder()
            .WithPage(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.HOLIDAY_LIST_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeExceedsMaximum()
    {
        var request = new RequestListHolidaysJsonBuilder()
            .WithPageSize(ListHolidaysFluentValidation.MaximumPageSize + 1)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.HOLIDAY_LIST_PAGE_SIZE_INVALID,
                1, ListHolidaysFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsZero()
    {
        var request = new RequestListHolidaysJsonBuilder()
            .WithPageSize(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.HOLIDAY_LIST_PAGE_SIZE_INVALID,
                1, ListHolidaysFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsAfterDateTo()
    {
        var request = new RequestListHolidaysJsonBuilder()
            .WithDateFrom(new DateTime(2025, 12, 31))
            .WithDateTo(new DateTime(2025, 1, 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.HOLIDAY_LIST_DATE_RANGE_INVALID);
    }
}
