using CommonTestUtilities.Requests;
using server.Application.UseCases.People;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;
namespace Validators.Test.People;

public class PeopleFluentValidationTest
{
    [Fact]
    public void ExplicitPayAndHourOpening_AreValidWithoutAttendanceOrAutomaticPolicy()
    {
        new PayItemFluentValidation().Validate(PeopleRequestBuilder.Pay(Guid.NewGuid(),Guid.NewGuid())).IsValid.ShouldBeTrue();
        new HourBankEntryFluentValidation().Validate(PeopleRequestBuilder.Hours(Guid.NewGuid())).IsValid.ShouldBeTrue();
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(0.001)]
    public void Pay_InvalidAmount_IsRejected(decimal amount)
    {
        var request = PeopleRequestBuilder.Pay(Guid.NewGuid(),Guid.NewGuid(),amount);
        var result = new PayItemFluentValidation().Validate(request);
        result.IsValid.ShouldBeFalse(); result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
    }
    [Fact]
    public void Pay_InvalidEnumAndMonth_AreRejected()
    {
        var request=PeopleRequestBuilder.Pay(Guid.NewGuid(),Guid.NewGuid()); request.Kind=(PayItemKind)99; request.SourceMonth=request.SourceMonth.AddDays(1);
        var result=new PayItemFluentValidation().Validate(request);
        result.Errors.ShouldContain(e=>e.PropertyName=="Kind");result.Errors.ShouldContain(e=>e.PropertyName=="SourceMonth");
    }
    [Theory]
    [InlineData(-1)] [InlineData(100.0001)] [InlineData(18.12345)]
    public void Rule_UnsupportedPrecisionOrRate_IsRejected(decimal rate)
    {
        new CommissionRuleFluentValidation().Validate(PeopleRequestBuilder.Rule(Guid.NewGuid(),rate)).IsValid.ShouldBeFalse();
    }
    [Fact]
    public void Rule_EmptyBasisOrReverseInterval_IsRejected()
    {
        var request=PeopleRequestBuilder.Rule(Guid.NewGuid());request.IncludeBolaoFee=false;request.IncludeSpecialFee=false;
        request.EffectiveTo=request.EffectiveFrom.AddDays(-1);
        var result=new CommissionRuleFluentValidation().Validate(request);result.IsValid.ShouldBeFalse();result.Errors.Count.ShouldBe(2);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(-0.00001)]
    public void HourPayment_RequiresNegativeHoursAtDeclaredPrecision(decimal value)
    {
        var request=PeopleRequestBuilder.Hours(Guid.NewGuid(),HourBankEntryKind.Payment,value);
        new HourBankEntryFluentValidation().Validate(request).IsValid.ShouldBeFalse();
    }
    [Fact]
    public void EvidenceAndCalendar_AreRequired()
    {
        var request=PeopleRequestBuilder.Category();request.Evidence=" ";request.PostedOn=request.PostedOn.AddHours(1);
        var result=new PayCategoryFluentValidation().Validate(request);result.Errors.Count.ShouldBe(2);
    }
}
