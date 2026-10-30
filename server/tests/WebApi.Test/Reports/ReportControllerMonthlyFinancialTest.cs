using System.Net;
using System.Text.Json;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M11 Phase 6: <c>GET /report/monthly-financial/{year}/{month}</c> on real PostgreSQL under a frozen branch clock —
/// a Terminal shared by two operators, an unlinked manager recorder, Rejected/Draft/recheck closes, renamed and retired
/// marked types, out-of-scope rows, a mid-month policy change, an old open segment and today's live segment, the
/// elapsed/current/future month rules, configuration states, parity with the source reports, roles and isolation.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerMonthlyFinancialTest(ServerWebApplicationFactory factory)
{
    // Branch-local today is 2026-10-20 at 10:00; September is the elapsed month, October the current one.
    private static readonly DateTime Now = new(2026, 10, 20, 10, 0, 0);
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record Fixture(
        Branch Branch,
        string ManagerToken,
        Account T1,
        Account T2,
        Operator O1,
        Operator O2,
        User ManagerUser,
        TransactionType Tarifa,
        TransactionType SobraBolao,
        TransactionType SobraFederal,
        Dictionary<string, DailyClose> Closes);

    private async Task<Fixture> SeedAsync(string label, bool markTypes = true)
    {
        var (manager, branch, _, managerToken) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        await factory.SeedSettingAsync(branch.Id, dailyTargetHours: 7.33m);
        await factory.SeedTimeEntryPolicyAsync(branch.Id, new DateTime(2026, 9, 10), dailyTargetHours: 6.00m);
        var variance = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var user1 = await factory.SeedUserAsync();
        var user2 = await factory.SeedUserAsync();
        var o1 = await factory.SeedOperatorAsync(branch.Id, "Ana", userId: user1.Id);
        var o2 = await factory.SeedOperatorAsync(branch.Id, "Bruno", userId: user2.Id);
        var t1 = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T1");
        var t2 = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T2");
        var bank = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Banco");
        var receita = await factory.SeedCategoryAsync(branch.Id, "Receita", Direction.In);
        var entradas = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var comerciais = await factory.SeedCategoryAsync(branch.Id, "Despesas Comerciais", Direction.Out);
        var tarifa = await factory.SeedTransactionTypeAsync(receita.Id, "Tarifa Bolão",
            monthlyFinancialRole: markTypes ? MonthlyFinancialRole.BolaoServiceFee : MonthlyFinancialRole.None);
        var operador = await factory.SeedTransactionTypeAsync(entradas.Id, "Operador");
        var sobraBolao = await factory.SeedTransactionTypeAsync(comerciais.Id, "Sobra de Bolão",
            monthlyFinancialRole: markTypes ? MonthlyFinancialRole.CommercialLeftover : MonthlyFinancialRole.None);
        var sobraFederal = await factory.SeedTransactionTypeAsync(comerciais.Id, "Sobra de Federal",
            monthlyFinancialRole: markTypes ? MonthlyFinancialRole.CommercialLeftover : MonthlyFinancialRole.None);

        var closes = new Dictionary<string, DailyClose>();
        async Task Close(string key, Account account, int day, DailyCloseStatus status, decimal? retained, Guid? recorderOperator,
            Guid? recorderUser, Guid? submitter, DateTime? recheck = null, Guid? submitterUser = null)
        {
            var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, new DateTime(2026, 9, day), status,
                submittedByOperatorId: submitter, recordedByOperatorId: recorderOperator, recordedByUserId: recorderUser,
                openingRecheckRequiredAt: recheck, submittedByUserId: submitterUser);
            if (retained is { } value)
                await factory.SeedDailyCloseItemAsync(close.Id, variance.Id, value);
            closes[key] = close;
        }

        // T1 is shared: O1 records, O2 submits. An unlinked manager records the Rejected close.
        await Close("approved", t1, 2, DailyCloseStatus.Approved, -12.5m, o1.Id, user1.Id, o2.Id);
        await Close("rejected", t1, 3, DailyCloseStatus.Rejected, 5m, null, manager.Id, null, submitterUser: manager.Id);
        await Close("recalled", t2, 3, DailyCloseStatus.Draft, 50m, o2.Id, user2.Id, null);
        await Close("recheck", t2, 4, DailyCloseStatus.Draft, 7m, o2.Id, user2.Id, null, recheck: DateTime.UtcNow);
        await Close("submitted", t1, 5, DailyCloseStatus.Submitted, 3m, o1.Id, user1.Id, o1.Id);

        async Task Row(TransactionType type, Category category, Direction direction, Account account, Operator recorder, int day,
            decimal value, TransactionStatus status = TransactionStatus.Active, bool legacy = false) =>
            await factory.SeedTransactionAsync(branch.Id, account.Id, type.Id, category.Id, direction, recorder.Id, manager.Id,
                date: new DateTime(2026, 9, day), value: value, status: status, legacyUnsessioned: legacy);

        await Row(tarifa, receita, Direction.In, t1, o1, 2, 120m);
        await Row(tarifa, receita, Direction.In, t1, o2, 2, 30m);
        await Row(tarifa, receita, Direction.In, t1, o1, 3, 100m);
        await Row(tarifa, receita, Direction.In, t2, o2, 6, 40m, legacy: true);   // fee day with no session (legacy)
        await Row(tarifa, receita, Direction.In, t1, o1, 2, 999m, TransactionStatus.Draft);
        await Row(tarifa, receita, Direction.In, t1, o1, 2, 888m, TransactionStatus.Cancelled);
        await Row(tarifa, receita, Direction.In, bank, o1, 2, 500m);               // bank inflow: out of scope
        await Row(operador, entradas, Direction.In, t1, o1, 2, 350m);              // Operador is never the fee
        await Row(sobraBolao, comerciais, Direction.Out, t1, o1, 2, 25m);
        await Row(sobraFederal, comerciais, Direction.Out, t1, o2, 2, 10m);
        await Row(tarifa, receita, Direction.In, t1, o1, 20, 0.01m, legacy: true); // a later fee-only legacy day on T1

        // Hours: a split day under the default policy, a day under the 10 September policy, an old forgotten clock-out,
        // and today's live segment.
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, o1.Id, new DateTime(2026, 9, 2),
            TimeEntryStatus.Present,
            (new DateTime(2026, 9, 2, 8, 0, 0), new DateTime(2026, 9, 2, 12, 0, 0), true),
            (new DateTime(2026, 9, 2, 13, 0, 0), new DateTime(2026, 9, 2, 17, 0, 0), true));
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, o2.Id, new DateTime(2026, 9, 15),
            TimeEntryStatus.Present, (new DateTime(2026, 9, 15, 9, 0, 0), new DateTime(2026, 9, 15, 15, 0, 0), true));
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, o2.Id, new DateTime(2026, 9, 21),
            TimeEntryStatus.Present, (new DateTime(2026, 9, 21, 8, 0, 0), null, true));
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, o1.Id, Now.Date,
            TimeEntryStatus.Present, (Now.Date.AddHours(8), null, true));

        return new Fixture(branch, managerToken, t1, t2, o1, o2, manager, tarifa, sobraBolao, sobraFederal, closes);
    }

    private async Task<ResponseMonthlyFinancialReportJson> Read(string token, int year, int month)
    {
        var response = await _client.GetAuthAsync($"/report/monthly-financial/{year}/{month}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseMonthlyFinancialReportJson>();
    }

    [Fact]
    public async Task ElapsedMonth_ShouldKeepCloseFiguresPerTerminalDay_HoursPerOperatorDay_AndOfficialValuesOnly()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var fixture = await SeedAsync("MonthlyFinancialElapsed");

        var report = await Read(fixture.ManagerToken, 2026, 9);

        report.DateFrom.ShouldBe(new DateTime(2026, 9, 1));
        report.DateTo.ShouldBe(new DateTime(2026, 9, 30));
        report.IsPartialMonth.ShouldBeFalse();
        report.AsOfDate.ShouldBe(Now.Date);
        report.BranchLocalNow.ShouldBe(Now);

        // Official variance: Approved −12.5, Rejected +5, Submitted +3; both Draft closes' retained 50/7 are suppressed.
        report.OfficialVarianceTotal.ShouldBe(-4.5m);
        report.OfficialCloseCount.ShouldBe(3);
        report.IsBolaoServiceFeeConfigured.ShouldBeTrue();
        report.BolaoServiceFeeTotal.ShouldBe(290.01m);
        report.IsCommercialLeftoverConfigured.ShouldBeTrue();
        report.CommercialLeftoverTotal.ShouldBe(35m);

        report.TerminalDays.Select(day => (day.Date.Day, day.AccountName, day.CloseStatus, day.OfficialVariance, day.BolaoServiceFee))
            .ShouldBe([
                (2, "T1", (DailyCloseStatus?)DailyCloseStatus.Approved, (decimal?)-12.5m, (decimal?)150m),
                (3, "T1", DailyCloseStatus.Rejected, 5m, 100m),
                (3, "T2", DailyCloseStatus.Draft, null, 0m),
                (4, "T2", DailyCloseStatus.Draft, null, 0m),
                (5, "T1", DailyCloseStatus.Submitted, 3m, 0m),
                (6, "T2", null, null, 40m),
                (20, "T1", null, null, 0.01m)
            ]);
        // The shared Terminal's fee is one figure for the Terminal/day, shown beside recorder and submitter.
        var shared = report.TerminalDays[0];
        shared.BolaoServiceFeeRowCount.ShouldBe(2);
        shared.RecordedByOperatorId.ShouldBe(fixture.O1.Id);
        shared.SubmittedByOperatorId.ShouldBe(fixture.O2.Id);
        var rejected = report.TerminalDays[1];
        rejected.RecordedByOperatorId.ShouldBeNull();
        rejected.RecordedByUserId.ShouldBe(fixture.ManagerUser.Id);
        rejected.SubmittedByOperatorId.ShouldBeNull();
        rejected.SubmittedByUserId.ShouldBe(fixture.ManagerUser.Id);

        report.Leftovers.Select(row => (row.RecordedByOperatorName, row.TransactionTypeName, row.Value))
            .ShouldBe([("Ana", "Sobra de Bolão", 25m), ("Bruno", "Sobra de Federal", 10m)]);
        report.LeftoversByType.Select(total => (total.TransactionTypeName, total.Total)).ShouldBe([
            ("Sobra de Bolão", 25m), ("Sobra de Federal", 10m)]);

        // Hours: 8.00 (split day, real 1 h gap absorbs the lunch), 5.75 (6 h gross under the 6.00 h target from the
        // 10 September policy), 0 for the forgotten clock-out, which stays in progress.
        report.Hours.Select(day => (day.Date.Day, day.OperatorName, day.TotalHours, day.BalanceHours, day.IsInProgress)).ShouldBe([
            (2, "Ana", 8.00m, 8.00m - 7.33m, false),
            (15, "Bruno", 5.75m, -0.25m, false),
            (21, "Bruno", 0m, -6.00m, true)
        ]);
        report.HoursTotal.ShouldBe(13.75m);
        report.ContainsInProgress.ShouldBeTrue();

        // Parity with the source reports over the same window.
        var cashVariance = await (await _client.GetAuthAsync(
                "/report/cash-variance?DateFrom=2026-09-01&DateTo=2026-09-30&Page=1&PageSize=200", fixture.ManagerToken))
            .ReadContentAsync<ResponseCashVarianceSummaryJson>();
        cashVariance.TotalVariance.ShouldBe(report.OfficialVarianceTotal);
        cashVariance.TotalCount.ShouldBe(report.OfficialCloseCount);
        var timeEntries = await (await _client.GetAuthAsync(
                "/report/timeentry-balance?DateFrom=2026-09-01&DateTo=2026-09-30", fixture.ManagerToken))
            .ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();
        timeEntries.Operators.Sum(op => op.TotalHours).ShouldBe(report.HoursTotal);
    }

    [Fact]
    public async Task CurrentMonth_ShouldEndAtToday_WithTodaysLiveSegmentProvisional_AndFutureMonthShouldBe400()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var fixture = await SeedAsync("MonthlyFinancialCurrent");

        var october = await Read(fixture.ManagerToken, 2026, 10);

        october.IsPartialMonth.ShouldBeTrue();
        october.DateFrom.ShouldBe(new DateTime(2026, 10, 1));
        october.DateTo.ShouldBe(Now.Date);
        var today = october.Hours.Single();
        today.OperatorId.ShouldBe(fixture.O1.Id);
        today.IsInProgress.ShouldBeTrue();
        today.TotalHours.ShouldBe(2.00m);
        today.BalanceHours.ShouldBe(-4.00m);
        october.HoursTotal.ShouldBe(2.00m);
        october.ContainsInProgress.ShouldBeTrue();
        october.TerminalDays.ShouldBeEmpty();
        october.BolaoServiceFeeTotal.ShouldBe(0m);

        // Advancing the frozen clock one hour moves only the live segment (still under the 4 h lunch tier).
        clock.AdvanceTo(Now.AddHours(1));
        (await Read(fixture.ManagerToken, 2026, 10)).HoursTotal.ShouldBe(3.00m);

        var november = await _client.GetAuthAsync("/report/monthly-financial/2026/11", fixture.ManagerToken);
        november.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await november.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);
        (await _client.GetAuthAsync("/report/monthly-financial/2026/13", fixture.ManagerToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.GetAuthAsync("/report/monthly-financial/1999/12", fixture.ManagerToken)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkedTypes_ShouldStayCountedWhenRenamedOrRetired_AndUnmarkedBranchShouldReportNotConfigured()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var fixture = await SeedAsync("MonthlyFinancialMarkers");

        var rename = await _client.PutAuthAsync($"/transaction-type/{fixture.SobraBolao.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Sobra de Bolão — cotas")
            .Build(), fixture.ManagerToken);
        rename.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.DeleteAuthAsync($"/transaction-type/{fixture.SobraFederal.Id}", fixture.ManagerToken)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAuthAsync($"/transaction-type/{fixture.Tarifa.Id}", fixture.ManagerToken)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var report = await Read(fixture.ManagerToken, 2026, 9);
        report.IsBolaoServiceFeeConfigured.ShouldBeTrue();
        report.BolaoServiceFeeTotal.ShouldBe(290.01m);
        report.CommercialLeftoverTotal.ShouldBe(35m);
        report.LeftoversByType.Select(total => (total.TransactionTypeName, total.TransactionTypeActive)).ShouldBe([
            ("Sobra de Bolão — cotas", true), ("Sobra de Federal", false)]);

        var unmarked = await SeedAsync("MonthlyFinancialUnmarked", markTypes: false);
        var notConfigured = await Read(unmarked.ManagerToken, 2026, 9);
        notConfigured.IsBolaoServiceFeeConfigured.ShouldBeFalse();
        notConfigured.BolaoServiceFeeTotal.ShouldBeNull();
        notConfigured.IsCommercialLeftoverConfigured.ShouldBeFalse();
        notConfigured.CommercialLeftoverTotal.ShouldBeNull();
        notConfigured.TerminalDays.ShouldAllBe(day => day.BolaoServiceFee == null);
        notConfigured.Leftovers.ShouldBeEmpty();
        // Variance and hours do not depend on the markers.
        notConfigured.OfficialVarianceTotal.ShouldBe(-4.5m);
        notConfigured.HoursTotal.ShouldBe(13.75m);
    }

    [Fact]
    public async Task ReclassifyingTypes_ShouldChangeLockedMonthReports_WithoutChangingFinancialHistory()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var fixture = await SeedAsync("MonthlyFinancialReclassification");
        await factory.UpdateDb(async db =>
            (await db.Settings.SingleAsync(setting => setting.BranchId == fixture.Branch.Id)).LockDate = new DateTime(2026, 9, 30));

        // Capture persisted financial state, including xmin, before changing only report metadata.
        async Task<string> FinancialHistory() => await factory.ReadDb(async db => JsonSerializer.Serialize(new
        {
            Transactions = await db.Transactions.Where(row => row.BranchId == fixture.Branch.Id).OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.Value, row.Direction, row.Date, row.DueDate, row.PaidAt, row.Status, row.Active, row.Version })
                .ToListAsync(),
            Closes = await db.DailyCloses.Where(row => row.BranchId == fixture.Branch.Id).OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.Status, row.Version }).ToListAsync(),
            Items = await db.DailyCloseItems.Where(row => row.DailyClose.BranchId == fixture.Branch.Id).OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.ProductId, row.Value, row.Active }).ToListAsync(),
            Settings = await db.Settings.Where(row => row.BranchId == fixture.Branch.Id)
                .Select(row => new { row.LockDate, row.Version }).ToListAsync()
        }));

        var history = await FinancialHistory();
        var before = await Read(fixture.ManagerToken, 2026, 9);
        before.BolaoServiceFeeTotal.ShouldBe(290.01m);
        before.CommercialLeftoverTotal.ShouldBe(35m);

        foreach (var type in new[] { fixture.Tarifa, fixture.SobraBolao, fixture.SobraFederal })
        {
            using var update = await _client.PutAuthAsync($"/transaction-type/{type.Id}", new RequestUpdateTransactionTypeJsonBuilder()
                .WithName(type.Name).WithMonthlyFinancialRole(MonthlyFinancialRole.None).Build(), fixture.ManagerToken);
            update.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var cleared = await Read(fixture.ManagerToken, 2026, 9);
        cleared.BolaoServiceFeeTotal.ShouldBeNull();
        cleared.CommercialLeftoverTotal.ShouldBeNull();
        cleared.OfficialVarianceTotal.ShouldBe(before.OfficialVarianceTotal);
        cleared.HoursTotal.ShouldBe(before.HoursTotal);
        using var overviewResponse = await _client.GetAuthAsync("/report/management-overview?Year=2026&Month=9", fixture.ManagerToken);
        overviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var overview = await overviewResponse.ReadContentAsync<ResponseManagementOverviewJson>();
        overview.Reconciliation.IsMonthLocked.ShouldBeTrue();
        overview.MonthlyFinancial.Status.ShouldBe(OverviewSectionStatus.NotConfigured);
        overview.MonthlyFinancial.OfficialVarianceTotal.ShouldBe(before.OfficialVarianceTotal);
        overview.MonthlyFinancial.HoursTotal.ShouldBe(before.HoursTotal);

        // Configuration is reversible even for a locked month; Operador remains a separate unmarked In type.
        using var restore = await _client.PutAuthAsync($"/transaction-type/{fixture.Tarifa.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName(fixture.Tarifa.Name).WithMonthlyFinancialRole(MonthlyFinancialRole.BolaoServiceFee).Build(), fixture.ManagerToken);
        restore.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restored = await Read(fixture.ManagerToken, 2026, 9);
        restored.BolaoServiceFeeTotal.ShouldBe(before.BolaoServiceFeeTotal);
        restored.CommercialLeftoverTotal.ShouldBeNull();

        // Only the fee marker is configured: one missing marker must not hide the configured section.
        using var restoredOverviewResponse = await _client.GetAuthAsync("/report/management-overview?Year=2026&Month=9", fixture.ManagerToken);
        restoredOverviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restoredOverview = await restoredOverviewResponse.ReadContentAsync<ResponseManagementOverviewJson>();
        restoredOverview.MonthlyFinancial.Status.ShouldBe(OverviewSectionStatus.Ready);
        restoredOverview.MonthlyFinancial.IsBolaoServiceFeeConfigured.ShouldBeTrue();
        restoredOverview.MonthlyFinancial.IsCommercialLeftoverConfigured.ShouldBeFalse();
        restoredOverview.MonthlyFinancial.BolaoServiceFeeTotal.ShouldBe(290.01m);
        restoredOverview.MonthlyFinancial.CommercialLeftoverTotal.ShouldBeNull();
        restoredOverview.MonthlyFinancial.OfficialVarianceTotal.ShouldBe(-4.5m);
        (await FinancialHistory()).ShouldBe(history);
    }

    [Fact]
    public async Task MonthlyFinancial_ShouldEnforceRolesAndBranchIsolation()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var fixture = await SeedAsync("MonthlyFinancialIsolation");
        var other = await SeedAsync("MonthlyFinancialIsolationOther");
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("MonthlyFinancialMember", Role.Member);

        var mine = await Read(fixture.ManagerToken, 2026, 9);
        mine.TerminalDays.ShouldAllBe(day => day.AccountId == fixture.T1.Id || day.AccountId == fixture.T2.Id);
        mine.Hours.ShouldAllBe(day => day.OperatorId == fixture.O1.Id || day.OperatorId == fixture.O2.Id);
        mine.TerminalDays.ShouldNotContain(day => other.Closes.Values.Select(close => close.Id).Contains(day.DailyCloseId ?? Guid.Empty));

        (await _client.GetAsync("/report/monthly-financial/2026/9")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.GetAuthAsync("/report/monthly-financial/2026/9", memberToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
