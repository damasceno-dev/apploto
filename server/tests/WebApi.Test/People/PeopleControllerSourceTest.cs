using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;
namespace WebApi.Test.People;

[Collection(ServerApiCollection.Name)]
public class PeopleControllerSourceTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client=factory.CreateClient();
    [Fact]
    public async Task LaterDeclaredOpening_ReconstructsCarryoverOnce_WithoutRewritingEarlierUnknownCutoff()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);
        var day=new DateTime(2026,9,10);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory,s.BranchId,s.Person.Id,day,
            TimeEntryStatus.Present,(day.AddHours(8),day.AddHours(18),true)); // +2 attendance hours
        var opening=PeopleRequestBuilder.Hours(s.Person.Id,value:10);
        opening.PostedOn=new DateTime(2026,10,1); // explicit September source anchor, first recognized in October
        await s.Create<ResponseHourBankEntryJson>(client,"hour-bank-entry",opening);
        var payment=PeopleRequestBuilder.Hours(s.Person.Id,HourBankEntryKind.Payment,-3);
        payment.SourceDate=payment.PostedOn=new DateTime(2026,10,2);
        await s.Create<ResponseHourBankEntryJson>(client,"hour-bank-entry",payment);
        var october=await s.Statement(client,10);
        october.HourBank.CarryOverHours.ShouldBe(12);october.HourBank.PeriodOpeningHours.ShouldBe(0);
        october.HourBank.PaymentHours.ShouldBe(-3);october.HourBank.ClosingBalanceHours.ShouldBe(9);
        var september=await s.Statement(client,extra:"&asOfDate=2026-09-30");
        september.HourBank.ClosingBalanceHours.ShouldBeNull();september.HourBank.Completeness.ShouldBe(PeopleSourceCompleteness.NotConfigured);
    }

    [Fact]
    public async Task OmittedZeroCapableAmounts_AreNotSilentlyAssumedToBeExplicitZero()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);
        var day=PeopleRequestBuilder.September;
        var missingRate=new {operatorId=s.Person.Id,effectiveFrom=day,effectiveTo=new DateTime(2026,9,30),includeBolaoFee=true,postedOn=day,reason="Explicit rule",evidence="Synthetic evidence"};
        var missingHours=new {operatorId=s.Person.Id,kind="Opening",sourceDate=day,postedOn=day,externalIdentity="missing-hours",reason="Explicit opening",evidence="Synthetic evidence"};
        var missingPrize=new {name="Synthetic draw",modality="Declared",contest="123",periodFrom=day,periodTo=day,postedOn=day,reason="Explicit draw",evidence="Synthetic evidence"};
        var missingBasis=new {operatorId=s.Person.Id,sourceDate=day,sourceRole="EnteredPrize",externalIdentity="missing-basis",postedOn=day,reason="Explicit source",evidence="Synthetic evidence"};
        foreach(var (route,body) in new (string,object)[]{("commission-rule",missingRate),("hour-bank-entry",missingHours),("special-draw",missingPrize),("commission-source",missingBasis)})
        {
            var response=await client.PostAuthAsync("/people/"+route,body,s.Token,Guid.NewGuid().ToString("N"));
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest,await response.Content.ReadAsStringAsync());
            (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public async Task ExplicitCommissionSource_CorrectionUsesOriginalRateAndPostingMonth_WithoutPayOrFinanceEffects()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);
        var rule=PeopleRequestBuilder.Rule(s.Person.Id);rule.IncludeExplicitSources=true;
        await s.Create<ResponseCommissionRuleJson>(client,"commission-rule",rule);
        var missing=await s.Statement(client);missing.Commission.ProposedAmount.ShouldBeNull();
        var source=new RequestCommissionSourceJson{OperatorId=s.Person.Id,SourceDate=PeopleRequestBuilder.September,PostedOn=PeopleRequestBuilder.September,
            SignedAmount=100,ExternalIdentity="fixture-prize-"+Guid.NewGuid().ToString("N"),SourceRole="EnteredPrize",Reason="Base explicitamente atribuída",Evidence="Documento sintético, sem rateio inventado"};
        var original=await s.Create<ResponseCommissionSourceJson>(client,"commission-source",source);
        var before=await s.Statement(client,extra:"&asOfDate=2026-09-30");before.Commission.ProposedAmount.ShouldBe(18.5m);
        (await client.PostAuthAsync("/people/commission-source",source,s.Token,Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await factory.ReadDb(async db=>{var setting=await db.Settings.SingleAsync(r=>r.BranchId==s.BranchId);setting.LockDate=new DateTime(2026,9,30);await db.SaveChangesAsync();return 0;});
        var octRule=PeopleRequestBuilder.Rule(s.Person.Id,20);octRule.IncludeExplicitSources=true;octRule.EffectiveFrom=octRule.PostedOn=new DateTime(2026,10,1);octRule.EffectiveTo=new DateTime(2026,10,31);
        await s.Create<ResponseCommissionRuleJson>(client,"commission-rule",octRule);
        source.PreviousId=original.Id;source.SignedAmount=80;source.PostedOn=new DateTime(2026,10,2);
        var changed=await client.PostAuthAsync($"/people/commission-source/{original.Id}/revision",source,s.Token,Guid.NewGuid().ToString("N"),original.Version);
        changed.StatusCode.ShouldBe(HttpStatusCode.Created,await changed.Content.ReadAsStringAsync());
        (await changed.ReadContentAsync<ResponseCommissionSourceJson>()).SignedContribution.ShouldBe(-20);
        var past=await s.Statement(client,extra:"&asOfDate=2026-09-30");past.Commission.CalculationId.ShouldBe(before.Commission.CalculationId);past.Commission.ProposedAmount.ShouldBe(18.5m);
        var current=await s.Statement(client,10);current.Commission.ProposedAmount.ShouldBe(-3.70m);current.Commission.Basis.Single().RatePercent.ShouldBe(18.5m);
        current.RecordedPaySignedTotal.ShouldBe(0);
        await factory.ReadDb(async db=>{(await db.Transactions.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(0);(await db.EmployeePayItems.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(0);return 0;});
    }
    [Fact]
    public async Task DuplicateHourProvenanceAndInactiveBankOnlyEmployee_AreRetainedWithoutMembershipOrAttendance()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);var opening=PeopleRequestBuilder.Hours(s.Person.Id,value:-2);
        await s.Create<ResponseHourBankEntryJson>(client,"hour-bank-entry",opening);
        (await client.PostAuthAsync("/people/hour-bank-entry",opening,s.Token,Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await factory.ReadDb(async db=>{var person=await db.Operators.SingleAsync(p=>p.Id==s.Person.Id);person.Active=false;await db.SaveChangesAsync();return 0;});
        var response=await client.GetAuthAsync("/report/timeentry-balance?dateFrom=2026-10-01&dateTo=2026-10-31",s.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK,await response.Content.ReadAsStringAsync());
        var summary=await response.ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();
        summary.HourBanks.ShouldNotBeNull();var bank=summary.HourBanks.Single(h=>h.OperatorId==s.Person.Id);
        bank.CarryOverHours.ShouldBe(-2);bank.ClosingBalanceHours.ShouldBe(-2);bank.AttendanceContributionHours.ShouldBe(0);
        await factory.ReadDb(async db=>{(await db.TimeEntries.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(0);(await db.BranchUsers.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(1);return 0;});
    }
}
