using server.Application.UseCases.Reports.FinancialResult;
using server.Application.UseCases.Reports.PersonalDifferences;
using server.Application.UseCases.Sessions.Adjustment;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports;

public class FinancialResultFluentValidationTest
{
    [Fact]
    public void ValidMonthlyRequests_ShouldPass()
    {
        new FinancialResultFluentValidation().Validate(new RequestFinancialResultJson { Year = 2026, Month = 10 }).IsValid.ShouldBeTrue();
        new PersonalDifferencesFluentValidation().Validate(new RequestPersonalDifferencesJson { Year = 2026, Month = 10 }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1999, 10, 1, 50)]
    [InlineData(2101, 10, 1, 50)]
    [InlineData(2026, 0, 1, 50)]
    [InlineData(2026, 13, 1, 50)]
    [InlineData(2026, 10, 0, 50)]
    [InlineData(2026, 10, 1, 201)]
    [InlineData(2026, 10, int.MaxValue, 200)]
    public void InvalidWindowsAndPages_ShouldFail(int year, int month, int page, int size)
    {
        new FinancialResultFluentValidation().Validate(new RequestFinancialResultJson { Year = year, Month = month, Page = page, PageSize = size }).IsValid.ShouldBeFalse();
        new PersonalDifferencesFluentValidation().Validate(new RequestPersonalDifferencesJson { Year = year, Month = month, Page = page, PageSize = size }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void UnknownClassification_ShouldFail()
    {
        new FinancialResultFluentValidation().Validate(new RequestFinancialResultJson { Year = 2026, Month = 10, FinancialClass = (FinancialClass)99 }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(OpeningDiscrepancyAttribution.Outgoing, true)]
    [InlineData(OpeningDiscrepancyAttribution.Incoming, true)]
    [InlineData(OpeningDiscrepancyAttribution.Neither, true)]
    [InlineData(OpeningDiscrepancyAttribution.Unattributed, false)]
    [InlineData((OpeningDiscrepancyAttribution)99, false)]
    public void AdjustmentTarget_ShouldRequireAnExplicitDecision(OpeningDiscrepancyAttribution target, bool expected)
    {
        new OpeningDiscrepancyAdjustmentFluentValidation().Validate(new RequestOpeningDiscrepancyAdjustmentJson
        {
            DiscrepancyId = Guid.NewGuid(), ExpectedVersion = 1, ComparisonRevision = 1,
            Target = target, PostedOn = new DateTime(2026, 10, 2), Reason = "Evidência conferida"
        }).IsValid.ShouldBe(expected);
    }
}
