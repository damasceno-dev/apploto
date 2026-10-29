using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
using static WebApi.Test.Reports.ManagementOverviewFixture;

namespace WebApi.Test.Reports;

/// <summary>
/// M11 Phase 7 acceptance scenarios A1–A12: each overview section equals its full report for the same scope and window
/// (quiescent fixture, frozen clock), with the recipe's literal expectations, deactivated entities and branch isolation.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerManagementOverviewParityTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<TResponse> Get<TResponse>(string uri, string token) where TResponse : class
    {
        var response = await _client.GetAuthAsync(uri, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{uri}: {await response.Content.ReadAsStringAsync()}");
        return await response.ReadContentAsync<TResponse>();
    }

    private static string Day(DateTime date) => date.ToString("yyyy-MM-dd");

    [Fact]
    public async Task EverySection_ShouldEqualItsFullReport_ForTheSameScopeAndWindow()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewParity");
        var token = f.ManagerToken; // an unlinked Manager reads the whole overview

        var overview = await Get<ResponseManagementOverviewJson>("/report/management-overview", token);

        overview.AsOfDate.ShouldBe(T);
        overview.BranchLocalNow.ShouldBe(Now);
        overview.Year.ShouldBe(2026);
        overview.Month.ShouldBe(10);
        overview.MonthDateFrom.ShouldBe(new DateTime(2026, 10, 1));
        overview.MonthDateTo.ShouldBe(T);
        overview.IsPartialMonth.ShouldBeTrue();

        // A1 — queue counts equal the Work Queue for AsOfDate.
        var queue = await Get<ResponseManagerQueueJson>($"/report/manager-queue?date={Day(T)}", token);
        overview.Queue.Status.ShouldBe(OverviewSectionStatus.Ready);
        overview.Queue.Date.ShouldBe(T);
        overview.Queue.PendingApprovalCount.ShouldBe(queue.PendingApprovalCount);
        overview.Queue.RejectedCount.ShouldBe(queue.Closes.Count(close => close.Status == DailyCloseStatus.Rejected));
        overview.Queue.NotSubmittedCount.ShouldBe(queue.NotSubmitted.Count);
        overview.Queue.NotSubmittedCount.ShouldBe(2);
        overview.Queue.BacklogSubmittedCount.ShouldBe(queue.Backlog.SubmittedCount);
        overview.Queue.BacklogRejectedCount.ShouldBe(queue.Backlog.RejectedCount);
        overview.Queue.BacklogDraftCount.ShouldBe(queue.Backlog.DraftCount);
        overview.Queue.OldestPendingDate.ShouldBe(queue.Backlog.OldestPendingDate);
        overview.Queue.UnlockedDraftTransactionCount.ShouldBe(queue.UnlockedDraftTransactionCount);
        overview.Queue.UnlockedDraftTransactionCount.ShouldBe(4); // 12 Sep, today's Draft row, Draft plan P3's two rows
        overview.Queue.PendingOperatorLinkRequestCount.ShouldBe(queue.PendingOperatorLinkRequestCount);

        // A2 — recent closes: 12 in the window, preview in GET /dailyclose order, Draft variance suppressed.
        var recent = overview.RecentCloses;
        recent.Status.ShouldBe(OverviewSectionStatus.Ready);
        recent.DateFrom.ShouldBe(T.AddDays(-6));
        recent.DateTo.ShouldBe(T);
        recent.TotalCount.ShouldBe(12);
        (recent.DraftCount, recent.SubmittedCount, recent.ApprovedCount, recent.RejectedCount).ShouldBe((9, 1, 1, 1));
        recent.PreviewLimit.ShouldBe(10);
        var list = await Get<ResponseListDailyClosesJson>(
            $"/session?DateFrom={Day(T.AddDays(-6))}&DateTo={Day(T)}&Page=1&PageSize=10", token);
        list.TotalCount.ShouldBe(recent.TotalCount);
        recent.Items.Select(item => item.DailyCloseId).ShouldBe(list.Items.Select(item => item.Id));
        recent.Items.ShouldNotContain(item => item.DailyCloseId == f.Closes["outside"].Id);
        var allRecent = await Get<ResponseListDailyClosesJson>(
            $"/session?DateFrom={Day(T.AddDays(-6))}&DateTo={Day(T)}&Page=1&PageSize=50", token);
        allRecent.Items.Select(item => item.Id).ShouldContain(f.Closes["recalled"].Id);
        recent.Items.Where(item => item.Status == DailyCloseStatus.Draft).ShouldAllBe(item => item.OfficialVariance == null);
        var rejected = recent.Items.SingleOrDefault(item => item.DailyCloseId == f.Closes["rejected"].Id);
        if (rejected is not null)
        {
            rejected.OfficialVariance.ShouldBe(3m);
            rejected.RecordedByOperatorId.ShouldBeNull();
        }

        // A3 — official variance for the month equals the full report: +10 −5 +3, the retained Draft 50/7 excluded.
        var variance = await Get<ResponseCashVarianceSummaryJson>(
            $"/report/cash-variance?DateFrom=2026-10-01&DateTo={Day(T)}&Page=1&PageSize=200", token);
        overview.CashVariance.Status.ShouldBe(OverviewSectionStatus.Ready);
        overview.CashVariance.TotalVariance.ShouldBe(8m);
        overview.CashVariance.TotalVariance.ShouldBe(variance.TotalVariance);
        overview.CashVariance.MeanVariance.ShouldBe(variance.MeanVariance);
        overview.CashVariance.MaxVariance.ShouldBe(variance.MaxVariance);
        overview.CashVariance.MinVariance.ShouldBe(variance.MinVariance);
        overview.CashVariance.TotalCount.ShouldBe(variance.TotalCount);
        (overview.CashVariance.SubmittedCount, overview.CashVariance.ApprovedCount, overview.CashVariance.RejectedCount)
            .ShouldBe((1, 1, 1));
        overview.CashVariance.LargestVariances.Select(item => item.VarianceValue).ShouldBe([10m, -5m, 3m]);

        // A4 — the current month is never lockable; readiness equals the full report.
        var reconciliation = await Get<ResponseMonthlyReconciliationJson>("/report/monthly-reconciliation/2026/10", token);
        overview.Reconciliation.IsMonthElapsed.ShouldBeFalse();
        overview.Reconciliation.LockReady.ShouldBe(reconciliation.LockReady);
        overview.Reconciliation.LockDate.ShouldBe(new DateTime(2026, 8, 31));
        overview.Reconciliation.DateTo.ShouldBe(new DateTime(2026, 10, 31));
        overview.Reconciliation.UnapprovedCloseBlockerCount.ShouldBe(
            reconciliation.Blockers.Count(blocker => blocker.Type == MonthlyReconciliationBlockerType.UnapprovedClose));

        // A5/A6 — debt 160 over 2 debtors, credit 30 over 1, B absent; aging equals the FIFO report.
        var balance = await Get<ResponseFiadoBalanceJson>("/report/fiado/balance", token);
        var aging = await Get<ResponseFiadoAgingJson>("/report/fiado/aging?Page=1&PageSize=200", token);
        var fiado = overview.Fiado;
        (fiado.DebtorCount, fiado.TotalDebt, fiado.CreditorCount, fiado.TotalCredit).ShouldBe((2, 160m, 1, 30m));
        (fiado.DebtorCount, fiado.TotalDebt, fiado.CreditorCount, fiado.TotalCredit)
            .ShouldBe((balance.DebtorCount, balance.TotalDebt, balance.CreditorCount, balance.TotalCredit));
        fiado.TopDebtors.Select(item => (item.ClientName, item.OutstandingTotal)).ShouldBe([("Cliente A", 100m), ("Cliente D", 60m)]);
        fiado.AgingOutstandingTotal.ShouldBe(aging.OutstandingTotal);
        fiado.AgingOutstandingTotal.ShouldBe(160m);
        fiado.OldestUnpaidDate.ShouldBe(aging.OldestUnpaidDate);
        fiado.OldestUnpaidDate.ShouldBe(new DateTime(2026, 9, 20));
        fiado.AgingByBucket.Select(bucket => (bucket.Bucket, bucket.Value)).ShouldBe(aging.ByBucket.Select(bucket => (bucket.Bucket, bucket.Value)));

        // A7 — open cheques equal the full report; Draft P3 and paid P5 excluded; a standalone cheque is its own group.
        var cheques = await Get<ResponseOpenChequeAgingJson>("/report/cheques/open-aging?Page=1&PageSize=5", token);
        var open = overview.OpenCheques;
        (open.TotalCount, open.OpenRowCount, open.OutstandingTotal).ShouldBe((4, 7, 700m));
        (open.TotalCount, open.OpenRowCount, open.OutstandingTotal)
            .ShouldBe((cheques.TotalCount, cheques.OpenRowCount, cheques.OutstandingTotal));
        open.ByBucket.Select(bucket => bucket.Value).ShouldBe(cheques.ByBucket.Select(bucket => bucket.Value));
        open.Items.Select(item => item.OriginTransactionId).ShouldBe(cheques.Items.Select(item => item.OriginTransactionId));
        open.Items.Single(item => item.OriginTransactionId == null).OldestOpenTransactionId.ShouldBe(f.StandaloneCheque);
        open.OldestOpenDueDate.ShouldBe(T.AddDays(-10));

        // A8 — upcoming settlements equal the full report for [T, T+13]; the P1 row due +5 sits in both sections.
        var upcoming = await Get<ResponseUpcomingSettlementsJson>(
            $"/report/upcoming-settlements?DueDateFrom={Day(T)}&DueDateTo={Day(T.AddDays(13))}&Page=1&PageSize=10", token);
        var settlements = overview.UpcomingSettlements;
        settlements.Status.ShouldBe(OverviewSectionStatus.Ready);
        (settlements.TotalCount, settlements.TotalValue, settlements.OverdueCount, settlements.OverdueValue)
            .ShouldBe((3, 150m, 2, 200m));
        (settlements.TotalCount, settlements.TotalValue, settlements.OverdueCount, settlements.OverdueValue)
            .ShouldBe((upcoming.TotalCount, upcoming.TotalValue, upcoming.OverdueCount, upcoming.OverdueValue));
        settlements.Items.Select(item => item.TransactionId).ShouldBe(upcoming.Items.Select(item => item.TransactionId));
        settlements.Items.Single(item => item.DueDate == T.AddDays(5)).OriginTransactionId.ShouldBe(f.P1);

        // A9 — every listed account equals its daily ledger for today; Draft only counted apart; T3 flagged.
        var accounts = overview.AccountActivity;
        accounts.Items.Select(item => item.AccountName).ShouldBe(["Bank1", "T1", "T2", "T3", "Tab1"]);
        accounts.TotalCount.ShouldBe(5);
        foreach (var account in accounts.Items)
        {
            var ledger = await Get<ResponseDailyLedgerJson>(
                $"/report/daily-ledger?AccountId={account.AccountId}&DateFrom={Day(T)}&DateTo={Day(T)}&Page=1&PageSize=200", token);
            (account.TotalIn, account.TotalOut, account.Net, account.ActiveCount)
                .ShouldBe((ledger.TotalIn, ledger.TotalOut, ledger.NetTotal, ledger.TotalCount), account.AccountName);
            account.IsActive.ShouldBe(ledger.AccountActive);
        }
        var t1 = accounts.Items.Single(item => item.AccountId == f.T1.Id);
        (t1.TotalIn, t1.TotalOut, t1.ActiveCount, t1.DraftCount, t1.LastActivityDate).ShouldBe((140m, 30m, 3, 1, (DateTime?)T));
        var t2 = accounts.Items.Single(item => item.AccountId == f.T2.Id);
        (t2.ActiveCount, t2.LastActivityDate).ShouldBe((0, (DateTime?)T.AddDays(-1)));
        accounts.Items.Single(item => item.AccountId == f.T3.Id).IsActive.ShouldBeFalse();

        // A10 — every listed operator equals operator-summary for today; generated share is a subset; O4 flagged.
        var operators = overview.OperatorActivity;
        operators.Items.Select(item => item.OperatorName).ShouldBe(["O1 Ana", "O2 Bruno", "O4 Davi"]);
        foreach (var op in operators.Items)
        {
            var summary = await Get<ResponseOperatorTransactionSummaryJson>(
                $"/report/operator-summary?OperatorId={op.OperatorId}&DateFrom={Day(T)}&DateTo={Day(T)}", token);
            (op.TransactionCount, op.TotalIn, op.TotalOut, op.GeneratedCount, op.GeneratedIn, op.GeneratedOut, op.IsActive)
                .ShouldBe((summary.TotalTransactionCount, summary.TotalInValue, summary.TotalOutValue,
                    summary.GeneratedTransactionCount, summary.GeneratedInValue, summary.GeneratedOutValue, summary.OperatorActive));
        }
        var o2 = operators.Items.Single(item => item.OperatorId == f.O2.Id);
        (o2.TransactionCount, o2.GeneratedCount, o2.GeneratedIn).ShouldBe((2, 1, 40m));
        operators.Items.Single(item => item.OperatorId == f.O4.Id).IsActive.ShouldBeFalse();

        // A11 — hours equal the time-entry roll-up for the window; live vs unresolved-open kept apart.
        var timeEntries = await Get<ResponseTimeEntryBalanceSummaryJson>($"/report/timeentry-balance?DateFrom=2026-10-01&DateTo={Day(T)}", token);
        var hours = overview.Hours;
        hours.TotalCount.ShouldBe(timeEntries.Operators.Count);
        hours.TotalCount.ShouldBe(3);
        (hours.LiveNowCount, hours.UnresolvedOpenCount, hours.ContainsInProgress).ShouldBe((1, 1, true));
        hours.PositiveBalanceHours.ShouldBe(timeEntries.Operators.Where(op => op.TotalBalanceHours > 0).Sum(op => op.TotalBalanceHours));
        hours.NegativeBalanceHours.ShouldBe(timeEntries.Operators.Where(op => op.TotalBalanceHours < 0).Sum(op => op.TotalBalanceHours));
        (hours.PositiveBalanceCount, hours.NegativeBalanceCount).ShouldBe((1, 2));
        foreach (var op in hours.MostBehind)
        {
            var source = timeEntries.Operators.Single(item => item.OperatorId == op.OperatorId);
            (op.TotalHours, op.TotalBalanceHours, op.IsActive, op.ContainsInProgress)
                .ShouldBe((source.TotalHours, source.TotalBalanceHours, source.OperatorActive, source.ContainsInProgress));
        }
        hours.MostBehind.Select(op => op.TotalBalanceHours).ShouldBeInOrder();
        hours.MostBehind.Single(op => op.OperatorId == f.O3.Id).IsActive.ShouldBeFalse();

        // A12 — monthly financial totals equal the full report.
        var monthly = await Get<ResponseMonthlyFinancialReportJson>("/report/monthly-financial/2026/10", token);
        var financial = overview.MonthlyFinancial;
        financial.Status.ShouldBe(OverviewSectionStatus.Ready);
        (financial.OfficialVarianceTotal, financial.OfficialCloseCount, financial.BolaoServiceFeeTotal, financial.CommercialLeftoverTotal,
                financial.HoursTotal, financial.ContainsInProgress)
            .ShouldBe((monthly.OfficialVarianceTotal, monthly.OfficialCloseCount, monthly.BolaoServiceFeeTotal,
                monthly.CommercialLeftoverTotal, monthly.HoursTotal, monthly.ContainsInProgress));
        (financial.BolaoServiceFeeTotal, financial.CommercialLeftoverTotal).ShouldBe(((decimal?)100m, (decimal?)20m));
        financial.HoursTotal.ShouldBe(timeEntries.Operators.Sum(op => op.TotalHours));

        // Isolation — nothing of the second branch appears anywhere in the body.
        var body = await (await _client.GetAuthAsync("/report/management-overview", token)).Content.ReadAsStringAsync();
        foreach (var id in f.OtherBranchIds)
            body.ShouldNotContain(id.ToString());
    }

    [Fact]
    public async Task PastMonth_ShouldReportItsBlockers_AndLockedMonthShouldReportLocked_EqualToTheFullReport()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewPastMonths");

        var september = await Get<ResponseManagementOverviewJson>("/report/management-overview?Year=2026&Month=9", f.AdminToken);
        var fullSeptember = await Get<ResponseMonthlyReconciliationJson>("/report/monthly-reconciliation/2026/9", f.AdminToken);
        september.IsPartialMonth.ShouldBeFalse();
        september.MonthDateTo.ShouldBe(new DateTime(2026, 9, 30));
        september.Reconciliation.IsMonthElapsed.ShouldBeTrue();
        september.Reconciliation.IsMonthLocked.ShouldBeFalse();
        september.Reconciliation.LockReady.ShouldBeFalse();
        september.Reconciliation.LockReady.ShouldBe(fullSeptember.LockReady);
        (september.Reconciliation.UnapprovedCloseBlockerCount, september.Reconciliation.DraftTransactionBlockerCount,
                september.Reconciliation.MissingExpectedCloseBlockerCount)
            .ShouldBe((
                fullSeptember.Blockers.Count(b => b.Type == MonthlyReconciliationBlockerType.UnapprovedClose),
                fullSeptember.Blockers.Count(b => b.Type == MonthlyReconciliationBlockerType.DraftTransactions),
                fullSeptember.Blockers.Count(b => b.Type == MonthlyReconciliationBlockerType.MissingExpectedClose)));
        september.Reconciliation.UnapprovedCloseBlockerCount.ShouldBeGreaterThan(0);
        september.Reconciliation.DraftTransactionBlockerCount.ShouldBeGreaterThan(0);
        september.Reconciliation.MissingExpectedCloseBlockerCount.ShouldBeGreaterThan(0);
        // Month sections follow the selector; point-in-time and day sections stay on today.
        september.CashVariance.DateFrom.ShouldBe(new DateTime(2026, 9, 1));
        september.CashVariance.TotalVariance.ShouldBe(1m);
        september.AccountActivity.Date.ShouldBe(T);
        september.Hours.LiveNowCount.ShouldBe(0);

        var august = await Get<ResponseManagementOverviewJson>("/report/management-overview?Year=2026&Month=8", f.AdminToken);
        august.Reconciliation.IsMonthLocked.ShouldBeTrue();
        august.Reconciliation.IsMonthElapsed.ShouldBeTrue();
    }

    [Fact]
    public async Task RetiringTheMarkedTypes_ShouldKeepTheirRowsReported_AndTheSectionReady()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewRetiredInstruments");
        var before = await Get<ResponseManagementOverviewJson>("/report/management-overview", f.ManagerToken);

        foreach (var type in new[] { f.Credit, f.Debit, f.Cheque })
            (await _client.DeleteAuthAsync($"/transaction-type/{type.Id}", f.ManagerToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = await Get<ResponseManagementOverviewJson>("/report/management-overview", f.ManagerToken);
        after.UpcomingSettlements.Status.ShouldBe(OverviewSectionStatus.Ready);
        (after.UpcomingSettlements.TotalCount, after.UpcomingSettlements.TotalValue, after.UpcomingSettlements.OverdueValue)
            .ShouldBe((before.UpcomingSettlements.TotalCount, before.UpcomingSettlements.TotalValue, before.UpcomingSettlements.OverdueValue));
        after.OpenCheques.TotalCount.ShouldBe(before.OpenCheques.TotalCount);
    }
}
