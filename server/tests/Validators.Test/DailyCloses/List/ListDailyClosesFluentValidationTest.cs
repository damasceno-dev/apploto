using CommonTestUtilities.Requests;
using server.Application.UseCases.DailyCloses.List;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.DailyCloses.List;

public class ListDailyClosesFluentValidationTest
{
    private readonly ListDailyClosesFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDefaultRequest()
    {
        var request = new RequestListDailyClosesJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsZero()
    {
        var request = new RequestListDailyClosesJsonBuilder()
            .WithPage(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsZero()
    {
        var request = new RequestListDailyClosesJsonBuilder()
            .WithPageSize(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_SIZE_INVALID, 1, ListDailyClosesFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsOverTheCap()
    {
        var request = new RequestListDailyClosesJsonBuilder()
            .WithPageSize(ListDailyClosesFluentValidation.MaximumPageSize + 1)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_SIZE_INVALID, 1, ListDailyClosesFluentValidation.MaximumPageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsUndefined()
    {
        var request = new RequestListDailyClosesJsonBuilder()
            .WithStatus((DailyCloseStatus)999)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_STATUS_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsAfterDateTo()
    {
        var request = new RequestListDailyClosesJsonBuilder()
            .WithDateFrom(new DateTime(2025, 3, 11))
            .WithDateTo(new DateTime(2025, 3, 10))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LIST_DATE_RANGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenMineAndOperatorIdAreCombined()
    {
        var request = new RequestListDailyClosesJsonBuilder()
            .WithMine(true)
            .WithOperatorId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }
}
