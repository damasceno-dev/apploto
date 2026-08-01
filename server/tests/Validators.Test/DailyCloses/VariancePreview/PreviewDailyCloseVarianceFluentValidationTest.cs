using CommonTestUtilities.Requests;
using server.Application.UseCases.DailyCloses.VariancePreview;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.DailyCloses.VariancePreview;

public class PreviewDailyCloseVarianceFluentValidationTest
{
    private readonly PreviewDailyCloseVarianceFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenCandidateItemsAreValid()
    {
        var request = new RequestDailyCloseVariancePreviewJson
        {
            Items = [new RequestUpsertDailyCloseItemJsonBuilder().Build()]
        };

        _validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenCandidateItemsAreMissing()
    {
        var request = new RequestDailyCloseVariancePreviewJson();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEMS_REQUIRED);
    }
}
