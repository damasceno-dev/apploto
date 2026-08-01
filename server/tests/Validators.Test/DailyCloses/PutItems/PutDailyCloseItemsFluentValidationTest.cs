using CommonTestUtilities.Requests;
using server.Application.UseCases.DailyCloses;
using server.Application.UseCases.DailyCloses.PutItems;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.DailyCloses.PutItems;

public class PutDailyCloseItemsFluentValidationTest
{
    private readonly PutDailyCloseItemsFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDefaultRequest()
    {
        var request = new RequestPutDailyCloseItemsJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenVersionIsMissing()
    {
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithVersion(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_VERSION_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNotesExceedMaxLength()
    {
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithNotes(new string('x', DailyCloseValidationExtensions.NotesMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.DAILYCLOSE_NOTES_LENGTH,
                DailyCloseValidationExtensions.NotesMaxLength));
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenNotesAreEmpty()
    {
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithNotes(string.Empty)
            .Build();

        _validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemsIsNull()
    {
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithItems((IReadOnlyList<server.Communication.Requests.RequestUpsertDailyCloseItemJson>?)null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEMS_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemHasEmptyProductId()
    {
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithItems(
                new RequestUpsertDailyCloseItemJsonBuilder()
                    .WithProductId(Guid.Empty)
                    .Build())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_ID_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemHasNegativeValue()
    {
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithItems(
                new RequestUpsertDailyCloseItemJsonBuilder()
                    .WithValue(-0.01m)
                    .Build())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_NEGATIVE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemValueHasMoreThanFourteenTotalDigits()
    {
        // 123456789012345.00 has 15 integer digits — exceeds numeric(14,2) integer capacity.
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithItems(
                new RequestUpsertDailyCloseItemJsonBuilder()
                    .WithValue(123456789012345.00m)
                    .Build())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_PRECISION);
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemValueHasMoreThanTwoDecimalPlaces()
    {
        // 1.234 has 3 decimal places — exceeds numeric(14,2) scale.
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithItems(
                new RequestUpsertDailyCloseItemJsonBuilder()
                    .WithValue(1.234m)
                    .Build())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_PRECISION);
    }

    [Fact]
    public void Validate_ShouldFail_WhenItemsContainDuplicateProductId()
    {
        var sharedProductId = Guid.NewGuid();
        var request = new RequestPutDailyCloseItemsJsonBuilder()
            .WithItems(
                new RequestUpsertDailyCloseItemJsonBuilder().WithProductId(sharedProductId).Build(),
                new RequestUpsertDailyCloseItemJsonBuilder().WithProductId(sharedProductId).Build())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE);
    }
}
