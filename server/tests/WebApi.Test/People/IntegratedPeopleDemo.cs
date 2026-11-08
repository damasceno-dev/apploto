using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
namespace WebApi.Test.People;

/// <summary>Explicit M9.8 inputs attached to the existing real session/late-responsibility/banking journey.</summary>
internal sealed class IntegratedPeopleDemo(ServerWebApplicationFactory factory, HttpClient client, Guid branch, Guid person, string token)
{
    private readonly DateTime september = new(2026,9,1);
    private string? originalCalculation;
    public async Task SeedSeptember(Guid feeSession)
    {
        await factory.ReadDb(async db =>
        {
            var employee = await db.Operators.SingleAsync(o => o.Id == person);
            employee.Name = "Pessoa sintética para demonstrativo mensal integrado, com nome extenso, custódia e histórico preservado";
            await db.SaveChangesAsync(); return 0;
        });
        var salary = await Create<ResponsePayCategoryJson>("pay-category", PeopleRequestBuilder.Category());
        var deduction = await Create<ResponsePayCategoryJson>("pay-category", PeopleRequestBuilder.Category(PayItemKind.Deduction));
        var commissionCategory = PeopleRequestBuilder.Category(); commissionCategory.Name = "Comissão";
        var commission = await Create<ResponsePayCategoryJson>("pay-category", commissionCategory);
        var rule = PeopleRequestBuilder.Rule(person); rule.IncludeBolaoFee = false;
        await Create<ResponseCommissionRuleJson>("commission-rule", rule);
        var salaryItem = PeopleRequestBuilder.Pay(person,salary.Id,1000);salaryItem.PostedOn = new DateTime(2026,9,30);
        var deductionItem = PeopleRequestBuilder.Pay(person,deduction.Id,50,PayItemKind.Deduction);deductionItem.PostedOn = new DateTime(2026,9,30);
        await Create<ResponsePayItemJson>("pay-item",salaryItem);
        await Create<ResponsePayItemJson>("pay-item",deductionItem);
        await Create<ResponseHourBankEntryJson>("hour-bank-entry",PeopleRequestBuilder.Hours(person));
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory,branch,person,new DateTime(2026,9,9),TimeEntryStatus.DayOff); // -8, initial anchor 10 -> 2
        var draw = await Create<ResponseSpecialDrawJson>("special-draw",new RequestSpecialDrawJson
        {
            Name="Sorteio sintético integrado",Modality="Modalidade explicitamente configurada",Contest="123",Prize=1000000,
            PeriodFrom=september,PeriodTo=new DateTime(2026,9,30),PostedOn=september,Reason="Catálogo sintético",Evidence="Documento sintético, sem mapeamento de cliente real"
        });
        var fee = await factory.ReadDb(db => db.SessionFieldEffects.AsNoTracking().SingleAsync(f => f.SessionId == feeSession && f.Field == SessionCloseField.SpecialFee));
        await Create<ResponseSpecialDrawAllocationJson>("special-draw-allocation",new RequestSpecialDrawAllocationJson
        {
            DrawId=draw.Id,SessionId=feeSession,SessionFieldEffectId=fee.Id,SourceRevision=fee.Revision,SourceDate=new DateTime(2026,9,10),
            Amount=10,PostedOn=new DateTime(2026,9,30),Reason="Distribuição documental parcial",Evidence="10 de 15 comprovados; os 5 restantes ficam sem distribuição"
        });
        var before = await Read(9,"&asOfDate=2026-09-30");
        before.Commission.KnownBasis.ShouldBe(15);before.Commission.ProposedAmount.ShouldBe(2.78m);
        before.RecordedPaySignedTotal.ShouldBe(950);before.HourBank.ClosingBalanceHours.ShouldBe(2);
        before.SpecialDraws.AllocatedFeeTotal.ShouldBe(10);before.SpecialDraws.UnallocatedFeeTotal.ShouldBe(5);
        before.SessionCount.ShouldBe(2);
        originalCalculation = before.Commission.CalculationId;
        var recorded = PeopleRequestBuilder.Pay(person,commission.Id,2.78m);recorded.PostedOn=new DateTime(2026,9,30);recorded.CommissionCalculationId=originalCalculation;
        var item = await Create<ResponsePayItemJson>("pay-item",recorded);
        item.CommissionEvidenceJson.ShouldNotBeNull();item.CommissionEvidenceJson!.ShouldContain("2026-09-30");item.CategoryName.ShouldBe("Comissão");
        (await Read(9,"&asOfDate=2026-09-30")).RecordedPaySignedTotal.ShouldBe(952.78m);
    }
    public async Task CrossOctober()
    {
        await factory.SeedTimeEntryPolicyAsync(branch,new DateTime(2026,10,1),dailyTargetHours:6,lunchDeductionOver6H:0,lunchDeductionOver4H:0);
        await factory.ReadDb(async db => {var setting=await db.Settings.SingleAsync(s=>s.BranchId==branch);setting.DailyTargetHours=6;await db.SaveChangesAsync();return 0;});
        var day=new DateTime(2026,10,2);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory,branch,person,day,TimeEntryStatus.Present,(day.AddHours(8),day.AddHours(15),true)); // +1 under the new policy
        var pay=PeopleRequestBuilder.Hours(person,HourBankEntryKind.Payment,-1);pay.SourceDate=pay.PostedOn=day;
        var adjustment=PeopleRequestBuilder.Hours(person,HourBankEntryKind.Adjustment,-2);adjustment.SourceDate=new DateTime(2026,9,9);adjustment.PostedOn=new DateTime(2026,10,3);
        await Create<ResponseHourBankEntryJson>("hour-bank-entry",pay);
        await Create<ResponseHourBankEntryJson>("hour-bank-entry",adjustment);
        var rule=PeopleRequestBuilder.Rule(person,20);rule.IncludeBolaoFee=false;rule.PostedOn=rule.EffectiveFrom=new DateTime(2026,10,1);rule.EffectiveTo=new DateTime(2026,10,31);
        await Create<ResponseCommissionRuleJson>("commission-rule",rule);
        var next=await Read(10);
        next.HourBank.CarryOverHours.ShouldBe(2);next.HourBank.AttendanceContributionHours.ShouldBe(1);
        next.HourBank.PaymentHours.ShouldBe(-1);next.HourBank.AdjustmentHours.ShouldBe(-2);next.HourBank.ClosingBalanceHours.ShouldBe(0);
        var old=await Read(9,"&asOfDate=2026-09-30");
        old.Commission.CalculationId.ShouldBe(originalCalculation);old.Commission.ProposedAmount.ShouldBe(2.78m);
        old.RecordedPaySignedTotal.ShouldBe(952.78m);old.HourBank.ClosingBalanceHours.ShouldBe(2);
    }
    public async Task VerifyAfterResponsibilityCorrections()
    {
        var old=await Read(9,"&asOfDate=2026-09-30");
        old.RecordedPaySignedTotal.ShouldBe(952.78m);old.Commission.CalculationId.ShouldBe(originalCalculation);
        old.SpecialDraws.AllocatedFeeTotal.ShouldBe(10);old.HourBank.ClosingBalanceHours.ShouldBe(2);
        var next=await Read(10);
        next.Differences.Entries.Count(e=>e.SourceKind is PersonalDifferenceSourceKind.OpeningDiscrepancyAdjustment or PersonalDifferenceSourceKind.OpeningDiscrepancyAdjustmentOffset).ShouldBe(2);
        next.Differences.People.Single().Shortage.ShouldBe(0);next.RecordedPaySignedTotal.ShouldBe(0);
        next.HourBank.ClosingBalanceHours.ShouldBe(0);next.AutomaticPayrollDeduction.ShouldBeFalse();
        await SavePrint(9,"/tmp/loto-m98-integrated-september.html");
        await SavePrint(10,"/tmp/loto-m98-integrated-october.html");
    }
    private async Task<T> Create<T>(string route,object request) where T:class
    {
        var response=await client.PostAuthAsync("/people/"+route,request,token,Guid.NewGuid().ToString("N"));
        response.StatusCode.ShouldBe(HttpStatusCode.Created,await response.Content.ReadAsStringAsync());return await response.ReadContentAsync<T>();
    }
    private async Task<ResponseEmployeeStatementJson> Read(int month,string suffix="")
    {
        var response=await client.GetAuthAsync($"/report/employee-statement?operatorId={person}&year=2026&month={month}"+suffix,token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK,await response.Content.ReadAsStringAsync());return await response.ReadContentAsync<ResponseEmployeeStatementJson>();
    }
    private async Task SavePrint(int month,string path)
    {
        var response=await client.GetAuthAsync($"/report/employee-statement/print?operatorId={person}&year=2026&month={month}",token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK,await response.Content.ReadAsStringAsync());await File.WriteAllTextAsync(path,await response.Content.ReadAsStringAsync());
    }
}
