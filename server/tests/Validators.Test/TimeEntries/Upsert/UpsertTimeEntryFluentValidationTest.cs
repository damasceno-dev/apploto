using CommonTestUtilities.Requests;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.TimeEntries.Upsert;

public class UpsertTimeEntryFluentValidationTest
{
    private readonly UpsertTimeEntryFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenStatusIsAbonadoAndNoClocks()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithStatus(TimeEntryStatus.Vacation)
            .WithNoClocks()
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenClocksAreMidnightCrossing()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithClockIn(new TimeOnly(22, 0))
            .WithClockOut(new TimeOnly(6, 0))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenOperatorIdIsEmpty()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_OPERATOR_ID_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateIsDefault()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithDate(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsOutOfEnumRange()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithStatus((TimeEntryStatus)999)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenBothClocksAreEqual()
    {
        var sameTime = new TimeOnly(9, 0);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithClockIn(sameTime)
            .WithClockOut(sameTime)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_CLOCKS_EQUAL);
    }

    [Fact]
    public void Validate_ShouldNotApplyClockEqualityRule_WhenOnlyOneClockIsPresent()
    {
        // Status-relative checks (Present requires both clocks; non-Present rejects clocks)
        // live in the use case, not the validator. The validator stays shape-only and only
        // compares clocks when both are present.
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
