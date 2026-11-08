using CommonTestUtilities.Requests;
using server.Application.Services.Reports;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models;
using Shouldly;
using Xunit;
namespace UseCases.Test.Services.Reports;

public class EmployeeCommissionCalculatorTest
{
    private static readonly DateTime Sep = PeopleRequestBuilder.September;
    private readonly Guid person = Guid.NewGuid();
    private EmployeeCommissionRule Rule(DateTime from, DateTime to, decimal rate, bool explicitBasis = false) => new()
    {
        OperatorId = person,
        EffectiveFrom = from,
        EffectiveTo = to,
        RatePercent = rate,
        PostedOn = Sep,
        IncludeBolaoFee = true,
        IncludeSpecialFee = true,
        IncludeExplicitSources = explicitBasis,
        Evidence = "Explicit synthetic rule"
    };
    private static EmployeeCommissionBasisSource Source(DateTime date, decimal value) => new(Guid.NewGuid(), Guid.NewGuid(), date, date, CommissionBasisComponent.BolaoServiceFee, value, ClassificationProvenance.Posting);

    [Fact]
    public void UpgradeClassificationIsNotProofOfOriginalPosting_AndCannotProduceVerifiedCommission()
    {
        var source = new EmployeeCommissionBasisSource(Guid.NewGuid(), Guid.NewGuid(), Sep, Sep,
            CommissionBasisComponent.BolaoServiceFee, 100, ClassificationProvenance.MigratedFromTypeAtUpgrade);
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [Rule(Sep, Sep, 18.5m)], [source], [], 1, 50);
        result.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete);
        result.ProposedAmount.ShouldBeNull();
        result.Basis.Single().Evidence.ShouldContain("não comprovada");
    }

    [Fact]
    public void MidMonthChange_SignedBasis_AndSingleBoundaryRounding_HaveIndependentGolden()
    {
        var first = Rule(Sep, Sep.AddDays(14), 18.5m);
        var second = Rule(Sep.AddDays(15), Sep.AddMonths(1).AddDays(-1), 20);
        // 100 * .185 + 50 * .20 - 10 * .20 = 26.50; stable IDs, never product-name classification.
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, new DateTime(2026, 9, 30), new DateTime(2026, 9, 30),
            [first, second], [Source(Sep, 100), Source(Sep.AddDays(15), 50), Source(Sep.AddDays(16), -10)], [], 1, 2);
        result.ProposedAmount.ShouldBe(26.50m); result.KnownBasis.ShouldBe(140);
        result.BasisSourceCount.ShouldBe(3); result.Basis.Count.ShouldBe(2); result.Rules.Count.ShouldBe(2);
        result.RepresentsPayment.ShouldBeFalse(); result.Completeness.ShouldBe(PeopleSourceCompleteness.Complete);
    }
    [Fact]
    public void DecimalProducts_AreRoundedOnce_AwayFromZero()
    {
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [Rule(Sep, Sep, 50)], [Source(Sep, .01m), Source(Sep, .01m)], [], 1, 50);
        result.ProposedAmount.ShouldBe(.01m); // per-source rounding would incorrectly produce .02
    }
    [Fact]
    public void MissingRuleOrExplicitEvidence_RemainsUnknown()
    {
        var empty = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [], [], [], 1, 50);
        empty.ProposedAmount.ShouldBeNull(); empty.Completeness.ShouldBe(PeopleSourceCompleteness.NotConfigured);
        var missing = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [Rule(Sep, Sep, 18.5m, true)], [Source(Sep, 100)], [], 1, 50);
        missing.ProposedAmount.ShouldBeNull(); missing.KnownBasis.ShouldBe(100);
        missing.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete);
    }
    [Fact]
    public void LaterAmendment_UsesPostingCutoff_NotCurrentSettings()
    {
        var old = Rule(Sep, Sep.AddDays(29), 18.5m);
        var amendment = new EmployeeCommissionRule
        {
            OperatorId = person,
            EffectiveFrom = old.EffectiveFrom,
            EffectiveTo = old.EffectiveTo,
            RatePercent = 20,
            IncludeBolaoFee = true,
            PostedOn = Sep.AddDays(20),
            PreviousId = old.Id
        };
        var rows = new[] { Source(Sep, 100) };
        var earlier = EmployeeCommissionCalculator.Calculate(person, Sep, Sep.AddDays(9), Sep.AddDays(9), [old, amendment], rows, [], 1, 50);
        var current = EmployeeCommissionCalculator.Calculate(person, Sep, Sep.AddDays(29), Sep.AddDays(29), [old, amendment], rows, [], 1, 50);
        earlier.ProposedAmount.ShouldBe(18.5m); earlier.Rules.Single().Id.ShouldBe(old.Id);
        current.ProposedAmount.ShouldBe(20m); current.Rules.Single().Id.ShouldBe(amendment.Id);
    }
    [Fact]
    public void ExplicitCorrection_UsesSourceDateRule_AndOnlyPostedDelta()
    {
        var sepRule = Rule(Sep, Sep.AddDays(29), 18.5m, true);
        var octRule = Rule(Sep.AddMonths(1), new DateTime(2026, 10, 31), 20);
        var original = new EmployeeCommissionSource { OperatorId = person, SourceDate = Sep, PostedOn = Sep, SignedAmount = 100, SignedContribution = 100, SourceRole = "EnteredPrize" };
        var correction = new EmployeeCommissionSource { OperatorId = person, SourceDate = Sep, PostedOn = Sep.AddMonths(1), SignedAmount = 80, SignedContribution = -20, PreviousId = original.Id, SourceRole = "EnteredPrize" };
        var result = EmployeeCommissionCalculator.Calculate(person, Sep.AddMonths(1), Sep.AddMonths(1), Sep.AddMonths(1), [sepRule, octRule], [], [original, correction], 1, 50);
        result.ProposedAmount.ShouldBe(-3.70m); result.KnownBasis.ShouldBe(-20);
        result.Basis.Single().RatePercent.ShouldBe(18.5m);
    }

    [Fact]
    public void MissingLastDayAndAmbiguousSourceRule_BlockVerifiedProposal()
    {
        var rule = Rule(Sep, Sep.AddDays(28), 10);
        var gap = EmployeeCommissionCalculator.Calculate(person, Sep, Sep.AddDays(29), Sep.AddDays(29), [rule], [], [], 1, 50);
        gap.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete);
        gap.ProposedAmount.ShouldBeNull();
        gap.MissingInputs.ShouldContain(x => x.Contains("todas as datas"));
        var overlap = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep,
            [Rule(Sep, Sep, 10), Rule(Sep, Sep, 20)], [Source(Sep, 100)], [], 1, 50);
        overlap.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete);
        overlap.Basis.Single().RuleId.ShouldBeNull();
        overlap.MissingInputs.ShouldContain(x => x.Contains("data da origem"));
    }

    [Fact]
    public void DeclaredBasisFlags_ExcludeKnownSources_WithoutInventingUnconfiguredInputs()
    {
        var rule = new EmployeeCommissionRule
        {
            OperatorId = person,
            EffectiveFrom = Sep,
            EffectiveTo = Sep,
            PostedOn = Sep,
            RatePercent = 10,
            IncludeSpecialFee = true
        };
        var included = new EmployeeCommissionBasisSource(Guid.NewGuid(), Guid.NewGuid(), Sep, Sep,
            CommissionBasisComponent.SpecialDrawServiceFee, 40, ClassificationProvenance.OpenPeriodReclassification);
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [rule], [Source(Sep, 100), included], [], 1, 50);
        result.ProposedAmount.ShouldBe(4);
        result.KnownBasis.ShouldBe(40);
        result.Basis.Single(x => x.SourceId == included.SourceId).Evidence.ShouldContain("revisada em período aberto");
        result.Basis.Single(x => x.SourceId != included.SourceId).RuleId.ShouldBeNull();
        result.BasisSourceCount.ShouldBe(2);
    }

    [Fact]
    public void ExplicitZeroIsEvidence_AndRoleLabelsAndOriginalDatesRemainVisible()
    {
        var prize = new EmployeeCommissionSource
        {
            OperatorId = person,
            SourceDate = Sep,
            PostedOn = Sep,
            SignedContribution = 0,
            SourceRole = "EnteredPrize",
            Evidence = "Prêmio documentado como zero"
        };
        var leftover = new EmployeeCommissionSource
        {
            OperatorId = person,
            SourceDate = Sep,
            PostedOn = Sep,
            SignedContribution = -1,
            SourceRole = "BolaoLeftover",
            Evidence = "Estorno documentado"
        };
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [Rule(Sep, Sep, 50, true)], [], [prize, leftover], 1, 50);
        result.ProposedAmount.ShouldBe(-.50m);
        result.Basis.Single(x => x.SourceId == prize.Id).Evidence.ShouldBe("Prêmio informado: Prêmio documentado como zero");
        result.Basis.Single(x => x.SourceId == leftover.Id).Evidence.ShouldBe("Sobra de Bolão: Estorno documentado");
        result.Basis.All(x => x.SourceDate == Sep && x.PostedOn == Sep).ShouldBeTrue();
    }

    [Fact]
    public void SourceOutsideRuleWindow_AndUnknownClassification_StayActionable()
    {
        var source = new EmployeeCommissionBasisSource(Guid.NewGuid(), Guid.NewGuid(), Sep.AddDays(-1), Sep,
            CommissionBasisComponent.BolaoServiceFee, 100, null);
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [Rule(Sep, Sep, 10)], [source], [], 1, 50);
        result.ProposedAmount.ShouldBeNull();
        result.Basis.Single().Evidence.ShouldBe("Classificação sem proveniência comprovada.");
        result.MissingInputs.ShouldContain(x => x.Contains(source.SourceId.ToString()) && x.Contains("classificação original"));
        result.MissingInputs.ShouldContain(x => x.Contains("data da origem"));
    }

    [Fact]
    public void RuleEvidenceIncludesCoveredEmptyDates_ExcludesUnrelatedFutureWindows_AndSortsHistory()
    {
        var first = Rule(Sep, Sep.AddDays(14), 10);
        var second = Rule(Sep.AddDays(15), Sep.AddDays(29), 20);
        var future = Rule(Sep.AddMonths(1), Sep.AddMonths(2), 30);
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep.AddDays(29), Sep.AddDays(29),
            [future, second, first], [], [], 1, 50);
        result.ProposedAmount.ShouldBe(0);
        result.Rules.Select(x => x.Id).ShouldBe([first.Id, second.Id]);
        result.DateFrom.ShouldBe(Sep); result.DateTo.ShouldBe(Sep.AddDays(29)); result.AsOfDate.ShouldBe(Sep.AddDays(29));
    }

    [Fact]
    public void PaginationNeverChangesAuthoritativeTotals_AndRetainsCustodyAndClassificationEvidence()
    {
        var rows = Enumerable.Range(0, 6).Select(i => Source(Sep.AddDays(i), i + 1)).ToArray();
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep.AddDays(5), Sep.AddDays(5),
            [Rule(Sep, Sep.AddDays(5), 10)], rows, [], 2, 2);
        result.ProposedAmount.ShouldBe(2.10m); result.KnownBasis.ShouldBe(21); result.BasisSourceCount.ShouldBe(6);
        result.Basis.Select(x => x.SourceId).ShouldBe([rows[2].SourceId, rows[3].SourceId]);
        result.Basis[0].SessionId.ShouldBe(rows[2].SessionId);
        result.Basis[0].ClassificationProvenance.ShouldBe(ClassificationProvenance.Posting);
        result.Basis[0].Evidence.ShouldBe("Classificação congelada na publicação; custódia oficial da sessão.");
    }

    [Fact]
    public void CalculationIdentity_IsStableAcrossPaging_ButChangesWithCutoffOrEvidence()
    {
        var rule = Rule(Sep, Sep.AddDays(29), 10); var source = Source(Sep, 100);
        var original = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [rule], [source], [], 1, 1);
        var otherPage = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [rule], [source], [], 2, 1);
        original.CalculationId.ShouldNotBeNullOrEmpty(); otherPage.CalculationId.ShouldBe(original.CalculationId);
        var later = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep.AddDays(1), [rule], [source], [], 1, 1);
        later.CalculationId.ShouldNotBe(original.CalculationId);
        var correction = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [rule], [source with { SignedAmount = 101 }], [], 1, 1);
        correction.CalculationId.ShouldNotBe(original.CalculationId);
    }
    [Fact]
    public void EconomicDateWithoutPublicationHistory_IsNotVerifiedCommissionEvidence()
    {
        var source = Source(Sep, 100) with { PostingHistoryComplete = false };
        var result = EmployeeCommissionCalculator.Calculate(person, Sep, Sep, Sep, [Rule(Sep, Sep, 18.5m)], [source], [], 1, 50);
        result.ProposedAmount.ShouldBeNull(); result.CalculationId.ShouldBeNull();
        result.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete);
        result.Basis.Single().PostingHistoryComplete.ShouldBeFalse();
        result.MissingInputs.ShouldContain(x => x.Contains("mês de publicação"));
    }
}
