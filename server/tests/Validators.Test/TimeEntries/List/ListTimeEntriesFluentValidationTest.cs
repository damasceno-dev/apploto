using server.Application.UseCases.TimeEntries.List;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.TimeEntries.List;

public class ListTimeEntriesFluentValidationTest
{
    private readonly ListTimeEntriesFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDefaultsAreUsed()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsLessThanOne()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson { Page = 0 });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_LIST_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsBelowMinimum()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson { PageSize = 0 });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.TIMEENTRY_LIST_PAGE_SIZE_INVALID,
                1,
                ListTimeEntriesFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeExceedsMaximum()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson
        {
            PageSize = ListTimeEntriesFluentValidation.MaximumPageSize + 1
        });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.TIMEENTRY_LIST_PAGE_SIZE_INVALID,
                1,
                ListTimeEntriesFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsAfterDateTo()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson
        {
            DateFrom = new DateTime(2026, 5, 10),
            DateTo = new DateTime(2026, 5, 1)
        });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_LIST_DATE_RANGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDateFromEqualsDateTo()
    {
        var sameDate = new DateTime(2026, 5, 5);
        var result = _validator.Validate(new RequestListTimeEntriesJson
        {
            DateFrom = sameDate,
            DateTo = sameDate
        });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenOnlyOneDateIsSet()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson
        {
            DateFrom = new DateTime(2026, 5, 1)
        });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsOutOfEnumRange()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson
        {
            Status = (TimeEntryStatus)999
        });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenMineIsCombinedWithExplicitOperatorId()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson
        {
            Mine = true,
            OperatorId = Guid.NewGuid()
        });

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenMineIsFalseAndOperatorIdIsSet()
    {
        var result = _validator.Validate(new RequestListTimeEntriesJson
        {
            Mine = false,
            OperatorId = Guid.NewGuid()
        });

        result.IsValid.ShouldBeTrue();
    }
}
