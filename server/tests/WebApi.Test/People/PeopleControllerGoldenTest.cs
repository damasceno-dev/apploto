using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;
namespace WebApi.Test.People;

[Collection(ServerApiCollection.Name)]
public class PeopleControllerGoldenTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();
    private static readonly DateTime Sep = PeopleRequestBuilder.September;

    [Fact]
    public async Task PrintableErrors_UseTheStableJsonEnvelopeAndNeverNegotiateHtmlForAnError()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s = await PeopleTestScenario.Seed(factory);
        var missing = await client.GetAuthAsync($"/report/employee-statement/print?operatorId={Guid.NewGuid()}&year=2026&month=9", s.Token);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await missing.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(server.Exceptions.ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        var invalid = await client.GetAuthAsync($"/report/employee-statement/print?operatorId={s.Person.Id}&year=2026&month=13", s.Token);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        invalid.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task Statement_GoldenMonth_ExplicitPay_NoShortageDeduction_MultiMonthHourCarryover_AndStableLockedCutoff()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 11, 6, 10, 0, 0));
        var s = await PeopleTestScenario.Seed(factory);
        var add = await s.Create<ResponsePayCategoryJson>(client, "pay-category", PeopleRequestBuilder.Category());
        var deduct = await s.Create<ResponsePayCategoryJson>(client, "pay-category", PeopleRequestBuilder.Category(PayItemKind.Deduction));
        await s.Create<ResponseCommissionRuleJson>(client, "commission-rule", PeopleRequestBuilder.Rule(s.Person.Id));
        await s.Create<ResponsePayItemJson>(client, "pay-item", PeopleRequestBuilder.Pay(s.Person.Id, add.Id, 1000));
        await s.Create<ResponsePayItemJson>(client, "pay-item", PeopleRequestBuilder.Pay(s.Person.Id, deduct.Id, 50, PayItemKind.Deduction));
        await s.Create<ResponseHourBankEntryJson>(client, "hour-bank-entry", PeopleRequestBuilder.Hours(s.Person.Id));
        var day = Sep.AddDays(9);
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, s.BranchId, s.Person.Id, day,
            TimeEntryStatus.Present, (day.AddHours(8), day.AddHours(18), true)); // 10 - 8 = +2
        var absence = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, s.BranchId, s.Person.Id, day.AddDays(1), TimeEntryStatus.DayOff); // -8
        var unknown = await factory.SeedOperatorAsync(s.BranchId);
        var cashbox = await factory.SeedAccountAsync(s.BranchId, AccountType.Terminal);
        var session = await factory.SeedDailyCloseAsync(s.BranchId, cashbox.Id, day, DailyCloseStatus.Approved, recordedByOperatorId: unknown.Id, submittedByOperatorId: unknown.Id, custodianOperatorId: s.Person.Id);
        var secondSession = await factory.SeedDailyCloseAsync(s.BranchId, cashbox.Id, day.AddDays(2), DailyCloseStatus.Approved,
            recordedByOperatorId: unknown.Id, submittedByOperatorId: unknown.Id, custodianOperatorId: s.Person.Id);
        await factory.EnsureCashVarianceProductAsync(s.BranchId);
        var variance = await factory.ReadDb(db => db.Products.SingleAsync(p => p.BranchId == s.BranchId && p.Name == "Diferença Caixa"));
        await factory.SeedDailyCloseItemAsync(session.Id, variance.Id, -90);
        await factory.SeedDailyCloseItemAsync(secondSession.Id, variance.Id, 5);
        var category = await factory.SeedCategoryAsync(s.BranchId, defaultDirection: Direction.In);
        var feeType = await factory.SeedTransactionTypeAsync(category.Id, monthlyFinancialRole: MonthlyFinancialRole.BolaoServiceFee, financialClass: FinancialClass.ShopFeesAndIncome);
        var row = await factory.SeedTransactionAsync(s.BranchId, cashbox.Id, feeType.Id, category.Id, Direction.In, unknown.Id, s.UserId, date: day, value: 100);
        // Session fixture may adopt a placeholder; explicitly assert authoritative custody, not recorder.
        await factory.ReadDb(async db =>
        {
            var sessionRow = await db.DailyCloses.SingleAsync(r => r.Id == row.SessionId);
            sessionRow.CustodianOperatorId.ShouldBe(s.Person.Id);
            return 0;
        });
        await factory.ReadDb(async db =>
        {
            var attempt = new SessionCountAttempt { BranchId = s.BranchId, SessionId = session.Id, AttemptNumber = 1,
                ConfirmationToken = Guid.NewGuid(), ConferredByUserId = s.UserId, ConferredByName = "Synthetic independent golden",
                CommandKey = Guid.NewGuid().ToString("N") };
            db.SessionCountAttempts.Add(attempt); await db.SaveChangesAsync();
            // Fixture setup only: declare the actual official attempt before inserting its guarded posting.
            await using var fixture = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"DailyCloses\" SET \"SubmittedAttemptId\"={attempt.Id} WHERE \"Id\"={session.Id}");
            await fixture.CommitAsync();
            db.EmployeeCommissionFeePostings.Add(new EmployeeCommissionFeePosting { BranchId = s.BranchId, OperatorId = s.Person.Id,
                SessionId = session.Id, AttemptId = attempt.Id, TransactionId = row.Id, SourceDate = day, PostedOn = day,
                Component = CommissionBasisComponent.BolaoServiceFee, SignedAmount = 100, ClassificationRevision = row.ClassificationRevision, ClassificationProvenance = ClassificationProvenance.Posting });
            await db.SaveChangesAsync(); return 0;
        });
        var first = await s.Statement(client, extra: "&asOfDate=2026-09-30");
        first.SessionCount.ShouldBe(2);
        first.Sessions.ShouldAllBe(row => row.CustodianOperatorId == s.Person.Id && row.RecorderOperatorId == unknown.Id && row.SubmitterOperatorId == unknown.Id);
        first.Attendance.TotalHours.ShouldBe(10); // attendance is once per day, not once per session
        first.Differences.People.Single().Overage.ShouldBe(5);
        first.RecordedPaySignedTotal.ShouldBe(950);
        first.Differences.People.Single().Shortage.ShouldBe(90);
        first.AutomaticPayrollDeduction.ShouldBeFalse(); first.RepresentsPayment.ShouldBeFalse();
        first.Commission.ProposedAmount.ShouldBe(18.50m);
        first.Commission.KnownBasis.ShouldBe(100);
        first.HourBank.ClosingBalanceHours.ShouldBe(4); // evidenced 10 + 2 - 8, independent literal
        first.HourBank.CarryOverHours.ShouldBeNull();
        var commissionPay = PeopleRequestBuilder.Pay(s.Person.Id, add.Id, 18.50m);
        commissionPay.CommissionCalculationId = first.Commission.CalculationId;
        commissionPay.PostedOn = new DateTime(2026,9,30);
        await s.Create<ResponsePayItemJson>(client, "pay-item", commissionPay);
        var recorded = await s.Statement(client, extra: "&asOfDate=2026-09-30");
        recorded.RecordedPaySignedTotal.ShouldBe(968.50m); // proposed commission not credited twice
        recorded.Commission.ProposedAmount.ShouldBe(18.50m);
        // A lock is a fixture prerequisite here; lock API races have separate coverage.
        await factory.ReadDb(async db => { var setting = await db.Settings.SingleAsync(r => r.BranchId == s.BranchId); setting.LockDate = new DateTime(2026,9,30); await db.SaveChangesAsync(); return 0; });
        var payment = PeopleRequestBuilder.Hours(s.Person.Id, HourBankEntryKind.Payment, -3);
        payment.SourceDate = payment.PostedOn = new DateTime(2026,10,2);
        await s.Create<ResponseHourBankEntryJson>(client, "hour-bank-entry", payment);
        var adjustment = PeopleRequestBuilder.Hours(s.Person.Id, HourBankEntryKind.Adjustment, -2);
        adjustment.SourceDate = day; adjustment.PostedOn = new DateTime(2026,10,3);
        await s.Create<ResponseHourBankEntryJson>(client, "hour-bank-entry", adjustment);
        await factory.SeedTimeEntryPolicyAsync(s.BranchId, new DateTime(2026,10,1), dailyTargetHours: 6, lunchDeductionOver6H: 0, lunchDeductionOver4H: 0);
        var octDay = new DateTime(2026,10,4);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, s.BranchId, s.Person.Id, octDay, TimeEntryStatus.Present, (octDay.AddHours(8), octDay.AddHours(15), true)); // +1
        var rule = PeopleRequestBuilder.Rule(s.Person.Id, 20); rule.PostedOn = rule.EffectiveFrom = new DateTime(2026,10,1); rule.EffectiveTo = new DateTime(2026,10,31);
        await s.Create<ResponseCommissionRuleJson>(client, "commission-rule", rule);
        var oct = await s.Statement(client, 10);
        oct.HourBank.CarryOverHours.ShouldBe(4); oct.HourBank.AttendanceContributionHours.ShouldBe(1);
        oct.HourBank.PaymentHours.ShouldBe(-3); oct.HourBank.AdjustmentHours.ShouldBe(-2); oct.HourBank.ClosingBalanceHours.ShouldBe(0);
        var past = await s.Statement(client, extra: "&asOfDate=2026-09-30");
        past.Commission.CalculationId.ShouldBe(recorded.Commission.CalculationId);
        past.RecordedPaySignedTotal.ShouldBe(968.50m); past.HourBank.ClosingBalanceHours.ShouldBe(4);
        (await factory.ReloadAsync<TimeEntry>(entry.Id))!.BalanceHours.ShouldBe(0); // ledger entries never mutate stored attendance checkpoint
        (await factory.ReloadAsync<TimeEntry>(absence.Id))!.Status.ShouldBe(TimeEntryStatus.DayOff);
        var financialCount = await factory.ReadDb(db => db.Transactions.CountAsync(t => t.BranchId == s.BranchId));
        financialCount.ShouldBe(1);
        var print = await client.GetAuthAsync($"/report/employee-statement/print?operatorId={s.Person.Id}&year=2026&month=9&asOfDate=2026-09-30", s.Token);
        print.StatusCode.ShouldBe(HttpStatusCode.OK, await print.Content.ReadAsStringAsync());
        var html = await print.Content.ReadAsStringAsync(); html.ShouldContain("Sal&#225;rio sint&#233;tico"); html.ShouldContain("968,50"); html.ShouldContain("18,50"); html.ShouldContain("Não comprova pagamento");
        await File.WriteAllTextAsync("/tmp/loto-m98-statement.html", html);
    }

    [Fact]
    public async Task NoOpeningOrRule_IsUnknown_NotVerifiedZero_AndInactiveUnlinkedHistoryRemainsReadable()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s = await PeopleTestScenario.Seed(factory);
        await factory.ReadDb(async db => { var person = await db.Operators.SingleAsync(r => r.Id == s.Person.Id); person.Active=false; await db.SaveChangesAsync(); return 0; });
        var report = await s.Statement(client);
        report.OperatorActive.ShouldBeFalse(); report.HourBank.Completeness.ShouldBe(PeopleSourceCompleteness.NotConfigured);
        report.HourBank.ClosingBalanceHours.ShouldBeNull(); report.Commission.ProposedAmount.ShouldBeNull();
        report.Commission.Completeness.ShouldBe(PeopleSourceCompleteness.NotConfigured);
        report.RecordedPaySignedTotal.ShouldBe(0);
    }
}
