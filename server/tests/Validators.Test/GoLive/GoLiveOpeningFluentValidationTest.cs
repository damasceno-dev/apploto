using server.Application.UseCases.GoLive.Openings;
using server.Communication.Requests;
using Shouldly;
using Xunit;

namespace Validators.Test.GoLive;

public class GoLiveOpeningFluentValidationTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(1000000000000)]
    public void Refuses_invalid_money(decimal value)
    {
        var request = Valid(); request.Items[0].Value = value;
        new GoLiveOpeningFluentValidation().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Accepts_known_debt_and_refuses_unknown_client()
    {
        var request = Valid();
        new GoLiveOpeningFluentValidation().Validate(request).IsValid.ShouldBeTrue();
        request.ClientId = null;
        new GoLiveOpeningFluentValidation().Validate(request).IsValid.ShouldBeFalse();
        request.IsCheque = true;
        new GoLiveOpeningFluentValidation().Validate(request).IsValid.ShouldBeTrue();
    }

    private static RequestGoLiveOpeningJson Valid() => new()
    {
        AccountId = Guid.NewGuid(), ClientId = Guid.NewGuid(), TransactionTypeId = Guid.NewGuid(), RecordedByOperatorId = Guid.NewGuid(),
        AsOfDate = new DateTime(2026, 11, 1), SourceReference = "synthetic", SourceEvidence = "Synthetic known source",
        Items = [new() { Value = 125.50m, DueDate = new DateTime(2026, 12, 1) }]
    };
}
