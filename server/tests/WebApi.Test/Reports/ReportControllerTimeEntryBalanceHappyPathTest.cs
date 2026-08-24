using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerTimeEntryBalanceHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // Manager queries an operator over a mixed-status period: Present computes
    // from segments, abonado credits DailyTarget, owing debits DailyTarget.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TimeEntryBalance_ShouldSummarizeMixedStatusPeriod_WhenManagerQueriesOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEBHappyMixed", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, "Operadora Alvo");

        var presentDay = SpLocalDate().AddDays(-5);
        var sundayDay = SpLocalDate().AddDays(-4);
        var dayOffDay = SpLocalDate().AddDays(-3);

        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, presentDay, TimeEntryStatus.Present,
            segments: [(presentDay.AddHours(8), presentDay.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, sundayDay, TimeEntryStatus.Sunday);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, dayOffDay, TimeEntryStatus.DayOff);

        var from = SpLocalDate().AddDays(-30);
        var to = SpLocalDate();
        var url = $"/report/timeentry-balance?operatorId={op.Id}&dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();

        var summary = payload.Operators.Single();
        summary.OperatorId.ShouldBe(op.Id);
        summary.OperatorName.ShouldBe("Operadora Alvo");
        summary.PresentDays.ShouldBe(1);
        summary.AbonadoDays.ShouldBe(1);
        summary.OwingDays.ShouldBe(1);
        summary.AbsentDays.ShouldBe(0);
        summary.ContainsInProgress.ShouldBeFalse();

        // Present 8 h + Sunday 7.33 h + DayOff 0 h.
        summary.TotalHours.ShouldBe(8m + 7.33m);
        // Present 0.67 + Sunday 0 + DayOff -7.33.
        summary.TotalBalanceHours.ShouldBe(0.67m - 7.33m);

        summary.Days.Count.ShouldBe(3);
        summary.Days.Single(d => d.Status == TimeEntryStatus.Present).TotalHours.ShouldBe(8m);
        summary.Days.Single(d => d.Status == TimeEntryStatus.Sunday).TotalHours.ShouldBe(7.33m);
        summary.Days.Single(d => d.Status == TimeEntryStatus.DayOff).BalanceHours.ShouldBe(-7.33m);
        // Repository orders by Date ascending.
        summary.Days.Select(d => d.Date).ShouldBe(summary.Days.Select(d => d.Date).OrderBy(d => d));
    }

    // -------------------------------------------------------------------------
    // In-progress current-day entry surfaces live-recomputed hours, bypassing
    // the stale persisted checkpoint, and flips ContainsInProgress.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TimeEntryBalance_ShouldRecomputeLiveRunning_ForInProgressCurrentDayEntry()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEBHappyLive", Role.Manager);
        var setting = await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var today = SpLocalDate();

        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, today, TimeEntryStatus.Present,
            segments: [(today.AddHours(0), null, true)]);
        // Deliberately stale checkpoint — the read path must recompute, not pass this through.
        await SetPersistedTotalsAsync(entry.Id, totalHours: 0m, balanceHours: -7.33m);

        var from = today.AddDays(-1);
        var to = today.AddDays(1);
        var url = $"/report/timeentry-balance?operatorId={op.Id}&dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();

        var summary = payload.Operators.Single();
        summary.ContainsInProgress.ShouldBeTrue();
        var day = summary.Days.Single(d => d.Date.Date == today.Date);
        day.IsInProgress.ShouldBeTrue();
        day.TotalHours.ShouldBeGreaterThan(0m);

        // The persisted checkpoint (0) was bypassed by the live recompute.
        var reload = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        reload.TotalHours.ShouldBe(0m);
        day.TotalHours.ShouldNotBe(reload.TotalHours);

        // The day's TotalHours matches the live-recompute path within clock-drift tolerance.
        var expected = await RecomputeLiveTotalHoursAsync(entry.Id, setting);
        Math.Abs(day.TotalHours - expected).ShouldBeLessThan(0.05m);
        summary.TotalHours.ShouldBe(day.TotalHours);
    }

    // -------------------------------------------------------------------------
    // Member self via Mine = true — only own operator's entries are summarized.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TimeEntryBalance_ShouldReturnOwnScopedSummary_WhenMemberUsesMine()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEBHappyMine", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var linkedOperator = await factory.SeedOperatorAsync(branch.Id, "Operadora Membro", userId: user.Id);
        var otherOperator = await factory.SeedOperatorAsync(branch.Id, "Operadora Ruído");

        var day = SpLocalDate().AddDays(-2);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, linkedOperator.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);

        // Another operator's entry on the same day must not leak into the member's summary.
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, otherOperator.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);

        var from = SpLocalDate().AddDays(-10);
        var to = SpLocalDate();
        var url = $"/report/timeentry-balance?mine=true&dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();

        var summary = payload.Operators.Single();
        summary.OperatorId.ShouldBe(linkedOperator.Id);
        summary.OperatorName.ShouldBe("Operadora Membro");
        summary.PresentDays.ShouldBe(1);
        summary.Days.Count.ShouldBe(1);
        summary.TotalHours.ShouldBe(8m);
    }

    // -------------------------------------------------------------------------
    // Branch isolation: another branch's entries never leak into the summary.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TimeEntryBalance_ShouldExcludeOtherBranchRows_WhenAnotherBranchHasActivity()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEBHappyIsolation", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var otherBranch = await factory.SeedBranchForOtherContextAsync("TEBHappyIsolation-other");
        await factory.SeedSettingAsync(otherBranch.Id);
        var otherOp = await factory.SeedOperatorAsync(otherBranch.Id);

        var day = SpLocalDate().AddDays(-2);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, otherBranch.Id, otherOp.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);

        var from = SpLocalDate().AddDays(-10);
        var to = SpLocalDate();
        var url = $"/report/timeentry-balance?operatorId={op.Id}&dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();

        var summary = payload.Operators.Single();
        summary.PresentDays.ShouldBe(1);
        summary.Days.Count.ShouldBe(1);
        summary.TotalHours.ShouldBe(8m);
    }

    // -------------------------------------------------------------------------
    // Manager omitting both operatorId and mine → branch-wide roll-up: one element
    // per active operator — zero-entry operators included with empty Days and zero
    // totals (M7.7 Phase 7.2) — ordered by name. Other-branch rows stay out.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TimeEntryBalance_ShouldRollUpEveryOperator_WhenManagerOmitsOperatorAndMine()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEBHappyBranchWide", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var operatorBravo = await factory.SeedOperatorAsync(branch.Id, "Bravo");
        var operatorAlfa = await factory.SeedOperatorAsync(branch.Id, "Alfa");
        // Same-branch active operator with no entries in the window — must appear zeroed.
        await factory.SeedOperatorAsync(branch.Id, "Charlie");

        var otherBranch = await factory.SeedBranchForOtherContextAsync("TEBHappyBranchWide-other");
        await factory.SeedSettingAsync(otherBranch.Id);
        var otherOp = await factory.SeedOperatorAsync(otherBranch.Id, "Ruído");

        var day = SpLocalDate().AddDays(-3);
        // Alfa: one Present (8 h) + one Sunday (abonado 7.33).
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, operatorAlfa.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, operatorAlfa.Id, day.AddDays(1), TimeEntryStatus.Sunday);
        // Bravo: one Present (8 h).
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, operatorBravo.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);
        // Other branch — must never appear.
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, otherBranch.Id, otherOp.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);

        var from = SpLocalDate().AddDays(-30);
        var to = SpLocalDate();
        var url = $"/report/timeentry-balance?dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();

        payload.Operators.Count.ShouldBe(3);
        payload.Operators[0].OperatorName.ShouldBe("Alfa");
        payload.Operators[1].OperatorName.ShouldBe("Bravo");
        payload.Operators[2].OperatorName.ShouldBe("Charlie");
        // The other branch's operator never leaks in.
        payload.Operators.Select(o => o.OperatorName).ShouldNotContain("Ruído");

        payload.Operators[0].PresentDays.ShouldBe(1);
        payload.Operators[0].AbonadoDays.ShouldBe(1);
        payload.Operators[0].TotalHours.ShouldBe(8m + 7.33m);

        payload.Operators[1].PresentDays.ShouldBe(1);
        payload.Operators[1].TotalHours.ShouldBe(8m);

        // The zero-entry operator appears with empty Days and zeroed totals so payroll
        // sees the employee who never clocked in (M7.7 Phase 7.2).
        payload.Operators[2].Days.ShouldBeEmpty();
        payload.Operators[2].TotalHours.ShouldBe(0m);
        payload.Operators[2].TotalBalanceHours.ShouldBe(0m);
        payload.Operators[2].PresentDays.ShouldBe(0);
        payload.Operators[2].ContainsInProgress.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // M7.7 Phase 7.3 headline: changing DailyTargetHours, LunchDeductionOver6H, AND
    // LunchDeductionOver4H today leaves every prior day's reported balance unchanged,
    // while today's own rows (the EffectiveFrom boundary date) already use the new
    // constants.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TimeEntryBalance_ShouldKeepHistoricalBalancesStable_WhenAllThreeConstantsChangeToday()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEBPolicyStable", Role.Manager);
        var setting = await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var today = SpLocalDate();

        // Historical days under the initial policy (target 7.33, lunch 1.0/0.25):
        // Present 08–17 → gross 9 − 1 = 8 h (balance 0.67, over-6h tier);
        // Present 08–13 → gross 5 − 0.25 = 4.75 h (balance −2.58, over-4h tier);
        // DayOff → balance −7.33 (target only). All three constants have a historical day.
        var presentDay = today.AddDays(-5);
        var dayOffDay = today.AddDays(-4);
        var lowerTierDay = today.AddDays(-3);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, presentDay, TimeEntryStatus.Present,
            segments: [(presentDay.AddHours(8), presentDay.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, dayOffDay, TimeEntryStatus.DayOff);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, lowerTierDay, TimeEntryStatus.Present,
            segments: [(lowerTierDay.AddHours(8), lowerTierDay.AddHours(13), true)]);
        // A closed entry on TODAY — the boundary date the new policy becomes effective on.
        var todayEntry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, today, TimeEntryStatus.Present,
            segments: [(today.AddHours(8), today.AddHours(17), true)]);

        var url = $"/report/timeentry-balance?operatorId={op.Id}" +
            $"&dateFrom={today.AddDays(-10):yyyy-MM-dd}&dateTo={today:yyyy-MM-dd}";

        var before = await (await _client.GetAuthAsync(url, token))
            .ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();
        var beforeSummary = before.Operators.Single();
        beforeSummary.Days.Single(d => d.Date.Date == presentDay).TotalHours.ShouldBe(8m);
        beforeSummary.Days.Single(d => d.Date.Date == dayOffDay).BalanceHours.ShouldBe(-7.33m);
        beforeSummary.Days.Single(d => d.Date.Date == lowerTierDay).TotalHours.ShouldBe(4.75m);
        beforeSummary.Days.Single(d => d.Date.Date == lowerTierDay).BalanceHours.ShouldBe(-2.58m);
        beforeSummary.Days.Single(d => d.Date.Date == today).BalanceHours.ShouldBe(0.67m);

        // Change ALL THREE constants through the real write path.
        var updateResponse = await _client.PutAuthAsync(
            "/setting",
            new RequestUpdateSettingJsonBuilder()
                .WithDailyTargetHours(8m)
                .WithLunchDeductionOver6H(2m)
                .WithLunchDeductionOver4H(0.5m)
                .Build(),
            token,
            setting.Version);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await (await _client.GetAuthAsync(url, token))
            .ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();
        var afterSummary = after.Operators.Single();

        // Yesterday and earlier: byte-for-byte stable under the old policy — including the
        // over-4h tier day (a regression using today's 0.5 h tier would report 4.5 h).
        afterSummary.Days.Single(d => d.Date.Date == presentDay).TotalHours.ShouldBe(8m);
        afterSummary.Days.Single(d => d.Date.Date == presentDay).BalanceHours.ShouldBe(0.67m);
        afterSummary.Days.Single(d => d.Date.Date == dayOffDay).TotalHours.ShouldBe(0m);
        afterSummary.Days.Single(d => d.Date.Date == dayOffDay).BalanceHours.ShouldBe(-7.33m);
        afterSummary.Days.Single(d => d.Date.Date == lowerTierDay).TotalHours.ShouldBe(4.75m);
        afterSummary.Days.Single(d => d.Date.Date == lowerTierDay).BalanceHours.ShouldBe(-2.58m);

        // Today (the boundary date): gross 9 − lunch 2 = 7 h against the new 8 h target.
        afterSummary.Days.Single(d => d.Date.Date == today).TotalHours.ShouldBe(7m);
        afterSummary.Days.Single(d => d.Date.Date == today).BalanceHours.ShouldBe(-1m);

        // Read-surface parity: GET /timeentry for today's CLOSED row recomputes under the
        // same entry-date policy as the report — no stale persisted checkpoint leaks out.
        var todayGetResponse = await _client.GetAuthAsync($"/timeentry/{todayEntry.Id}", token);
        todayGetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var todayGetPayload = await todayGetResponse.ReadContentAsync<ResponseTimeEntryJson>();
        todayGetPayload.TotalHours.ShouldBe(7m);
        todayGetPayload.BalanceHours.ShouldBe(-1m);
        todayGetPayload.IsInProgress.ShouldBeFalse();

        // The change is recorded as a policy row effective from the branch-local today,
        // alongside the untouched MinValue initial row.
        using var scope = factory.Services.CreateScope();
        var policiesRepository = scope.ServiceProvider.GetRequiredService<ITimeEntryPoliciesRepository>();
        var policies = await policiesRepository.ListActiveByBranchIdAsNoTracking(branch.Id);
        policies.Count.ShouldBe(2);
        policies[0].EffectiveFrom.ShouldBe(DateTime.MinValue);
        policies[0].DailyTargetHours.ShouldBe(7.33m);
        policies[1].EffectiveFrom.ShouldBe(today);
        policies[1].DailyTargetHours.ShouldBe(8m);
        policies[1].LunchDeductionOver6H.ShouldBe(2m);
        policies[1].LunchDeductionOver4H.ShouldBe(0.5m);
    }

    // -------------------------------------------------------------------------
    // M7.7 Phase 7.3: the branch roll-up carries a LINKED zero-entry operator and an
    // active operator with NO login link at all (UserId = null) — payroll covers
    // employees, not logins.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TimeEntryBalance_ShouldIncludeLinkedAndUnlinkedZeroEntryOperators_InBranchWideRollUp()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEBPolicyZeroEntry", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var workedOperator = await factory.SeedOperatorAsync(branch.Id, "Beto");
        // Linked zero-entry operator (has a login link, never clocked in).
        await factory.SeedOperatorAsync(branch.Id, "Aline", userId: user.Id);
        // Unlinked zero-entry operator (no login link at all).
        await factory.SeedOperatorAsync(branch.Id, "Zeca", userId: null);

        var day = SpLocalDate().AddDays(-2);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, workedOperator.Id, day, TimeEntryStatus.Present,
            segments: [(day.AddHours(8), day.AddHours(17), true)]);

        var from = SpLocalDate().AddDays(-10);
        var to = SpLocalDate();
        var url = $"/report/timeentry-balance?dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryBalanceSummaryJson>();

        payload.Operators.Count.ShouldBe(3);
        payload.Operators.Select(o => o.OperatorName).ShouldBe(["Aline", "Beto", "Zeca"]);

        foreach (var idle in new[] { payload.Operators[0], payload.Operators[2] })
        {
            idle.Days.ShouldBeEmpty();
            idle.TotalHours.ShouldBe(0m);
            idle.TotalBalanceHours.ShouldBe(0m);
            idle.PresentDays.ShouldBe(0);
            idle.AbsentDays.ShouldBe(0);
            idle.OwingDays.ShouldBe(0);
            idle.AbonadoDays.ShouldBe(0);
            idle.ContainsInProgress.ShouldBeFalse();
        }

        payload.Operators[1].PresentDays.ShouldBe(1);
        payload.Operators[1].TotalHours.ShouldBe(8m);
    }

    private async Task SetPersistedTotalsAsync(Guid timeEntryId, decimal totalHours, decimal balanceHours)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var row = await dbContext.TimeEntries.SingleAsync(te => te.Id == timeEntryId);
        row.TotalHours = totalHours;
        row.BalanceHours = balanceHours;
        await dbContext.SaveChangesAsync();
    }

    private async Task<decimal> RecomputeLiveTotalHoursAsync(Guid timeEntryId, Setting setting)
    {
        using var scope = factory.Services.CreateScope();
        var timeEntriesRepository = scope.ServiceProvider.GetRequiredService<ITimeEntriesRepository>();
        var calculationService = scope.ServiceProvider.GetRequiredService<ITimeEntryCalculationService>();
        var branchClock = scope.ServiceProvider.GetRequiredService<IBranchClock>();

        var entry = await timeEntriesRepository.GetByIdAndBranchIdAsNoTracking(timeEntryId, setting.BranchId);
        entry.ShouldNotBeNull();

        var branchLocalNow = branchClock.LocalBusinessDateTime(branchClock.UtcNow());
        var segments = entry.Segments
            .Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut))
            .ToList();

        var (totalHours, _) = calculationService.Calculate(
            entry.Status,
            segments,
            entry.Date,
            branchLocalNow,
            setting.DailyTargetHours,
            setting.LunchDeductionOver6H,
            setting.LunchDeductionOver4H);

        return totalHours;
    }

    private static DateTime SpLocalDate()
    {
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date, DateTimeKind.Unspecified);
    }
}
