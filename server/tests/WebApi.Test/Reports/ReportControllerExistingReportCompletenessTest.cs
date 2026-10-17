using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M11 Phase 2.5: whole-filter open-cheque totals, cash-variance status counts, monthly lock context and the
/// historical (deactivated) entity drill-downs, against real PostgreSQL.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerExistingReportCompletenessTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly DateTime _today = new BranchClock().LocalBusinessDate(DateTime.UtcNow);

    [Fact]
    public async Task OpenChequeAging_ShouldReportWholeFilterTotals_EqualToTheSumOverEveryPage()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("M11OpenChequeTotals");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var bank = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var otherBank = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var cheque = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var asOf = new DateTime(2025, 6, 30);

        async Task Plan(Guid accountId, int dueOffset, params (decimal Value, TransactionStatus Status, bool Paid)[] rows)
        {
            var origin = Guid.NewGuid();
            for (var i = 0; i < rows.Length; i++)
            {
                await factory.SeedTransactionAsync(
                    branch.Id, accountId, cheque.Id, category.Id, Direction.Out, op.Id, user.Id,
                    date: new DateTime(2025, 4, 1), value: rows[i].Value,
                    dueDate: asOf.AddDays(dueOffset + 30 * i), originTransactionId: origin, status: rows[i].Status,
                    paidAt: rows[i].Paid ? new DateTime(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc) : null,
                    id: i == 0 ? origin : Guid.NewGuid());
            }
        }

        // Seven open plans (three pages of three): their unpaid Active rows are the open set.
        for (var plan = 0; plan < 7; plan++)
            await Plan(bank.Id, -95 + 10 * plan, (100m, TransactionStatus.Active, plan == 0), (50m, TransactionStatus.Active, false));
        // Excluded: a fully paid plan, a Draft plan, a cancelled installment, and another account's plan (filtered).
        await Plan(bank.Id, -10, (40m, TransactionStatus.Active, true), (40m, TransactionStatus.Active, true));
        await Plan(bank.Id, -10, (40m, TransactionStatus.Draft, false), (40m, TransactionStatus.Draft, false));
        await Plan(bank.Id, 5, (70m, TransactionStatus.Active, false), (999m, TransactionStatus.Cancelled, false));
        await Plan(otherBank.Id, 5, (500m, TransactionStatus.Active, false));

        var pages = new List<ResponseOpenChequeAgingJson>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await _client.GetAuthAsync(
                $"/report/cheques/open-aging?accountId={bank.Id}&asOfDate={asOf:yyyy-MM-dd}&page={page}&pageSize=3", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            pages.Add(await response.ReadContentAsync<ResponseOpenChequeAgingJson>());
        }

        var groups = pages.SelectMany(page => page.Items).ToList();
        pages.Select(page => page.Items.Count).ShouldBe([3, 3, 2]);
        pages.ShouldAllBe(page => page.TotalCount == 8);
        var expectedTotal = groups.Sum(group => group.OutstandingTotal);
        expectedTotal.ShouldBe(7 * 50m + 6 * 100m + 70m);
        pages.ShouldAllBe(page => page.OutstandingTotal == expectedTotal);
        pages.ShouldAllBe(page => page.OpenRowCount == groups.Sum(group => group.OpenRowCount));
        pages[0].ByBucket.Sum(bucket => bucket.Value).ShouldBe(expectedTotal);
        pages[0].ByBucket.Sum(bucket => bucket.RowCount).ShouldBe(pages[0].OpenRowCount);
        var rowsByBucket = groups.SelectMany(group => group.Rows).GroupBy(row => row.Bucket)
            .ToDictionary(g => g.Key, g => g.Sum(row => row.Value));
        foreach (var bucket in pages[0].ByBucket)
            bucket.Value.ShouldBe(rowsByBucket.GetValueOrDefault(bucket.Bucket));
    }

    [Fact]
    public async Task CashVarianceSummary_ShouldCountOfficialRowsByStatus_OverEveryPage()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("M11VarianceCounts");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var variance = await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        var statuses = new[]
        {
            DailyCloseStatus.Submitted, DailyCloseStatus.Approved, DailyCloseStatus.Approved,
            DailyCloseStatus.Rejected, DailyCloseStatus.Submitted, DailyCloseStatus.Approved, DailyCloseStatus.Draft
        };
        for (var day = 0; day < statuses.Length; day++)
        {
            var close = await factory.SeedDailyCloseAsync(branch.Id, terminal.Id, new DateTime(2025, 5, day + 1), statuses[day]);
            await factory.SeedDailyCloseItemAsync(close.Id, variance.Id, day + 1);
        }

        var pages = new List<ResponseCashVarianceSummaryJson>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await _client.GetAuthAsync(
                $"/report/cash-variance?dateFrom=2025-05-01&dateTo=2025-05-31&page={page}&pageSize=2", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            pages.Add(await response.ReadContentAsync<ResponseCashVarianceSummaryJson>());
        }

        pages.ShouldAllBe(page => page.Items.Count > 0);
        pages.ShouldAllBe(page => page.TotalCount == 6 && page.SubmittedCount == 2 && page.ApprovedCount == 3 && page.RejectedCount == 1);
        pages.ShouldAllBe(page => page.TotalVariance == 1m + 2m + 3m + 4m + 5m + 6m);
        pages.SelectMany(page => page.Items).Count().ShouldBe(6);
    }

    [Fact]
    public async Task MonthlyReconciliation_ShouldReportLockContext_ForLockedElapsedAndCurrentMonths()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("M11LockContext");
        var lockedMonthEnd = new DateTime(_today.Year, _today.Month, 1).AddMonths(-3).AddDays(-1);
        await factory.SeedSettingAsync(branch.Id, lockDate: lockedMonthEnd);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        var elapsed = new DateTime(_today.Year, _today.Month, 1).AddMonths(-1);

        var locked = await Monthly(token, lockedMonthEnd.Year, lockedMonthEnd.Month);
        var unlockedElapsed = await Monthly(token, elapsed.Year, elapsed.Month);
        var current = await Monthly(token, _today.Year, _today.Month);

        locked.LockDate.ShouldBe(lockedMonthEnd);
        locked.IsMonthLocked.ShouldBeTrue();
        locked.IsMonthElapsed.ShouldBeTrue();
        unlockedElapsed.IsMonthLocked.ShouldBeFalse();
        unlockedElapsed.IsMonthElapsed.ShouldBeTrue();
        current.IsMonthLocked.ShouldBeFalse();
        current.IsMonthElapsed.ShouldBeFalse();

        var (_, neverLockedBranch, _, neverLockedToken) = await factory.SeedFullBranchContextAsync("M11LockContextNever");
        await factory.SeedSettingAsync(neverLockedBranch.Id);
        await factory.SeedProductAsync(neverLockedBranch.Id, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        (await Monthly(neverLockedToken, elapsed.Year, elapsed.Month)).LockDate.ShouldBeNull();
    }

    [Fact]
    public async Task DrillDowns_ShouldOpenADeactivatedAccountOrOperator_AndFlagIt()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("M11HistoricalEntities");
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, "Operador antigo");
        var retiredAccount = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Conta encerrada", active: false);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var type = await factory.SeedTransactionTypeAsync(category.Id);
        var day = _today.AddDays(-3);
        await factory.SeedTransactionAsync(branch.Id, retiredAccount.Id, type.Id, category.Id, Direction.Out, op.Id, user.Id, date: day, value: 42m);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, day, TimeEntryStatus.Present, segments: [(day.AddHours(8), day.AddHours(12), true)]);
        await DeactivateOperatorAsync(op.Id);
        var window = $"dateFrom={day.AddDays(-1):yyyy-MM-dd}&dateTo={_today:yyyy-MM-dd}";

        var ledger = await Get<ResponseDailyLedgerJson>($"/report/daily-ledger?accountId={retiredAccount.Id}&{window}", token);
        var summary = await Get<ResponseOperatorTransactionSummaryJson>($"/report/operator-summary?operatorId={op.Id}&{window}", token);
        var balance = await Get<ResponseTimeEntryBalanceSummaryJson>($"/report/timeentry-balance?operatorId={op.Id}&{window}", token);
        var rollUp = await Get<ResponseTimeEntryBalanceSummaryJson>($"/report/timeentry-balance?{window}", token);

        ledger.AccountActive.ShouldBeFalse();
        ledger.Items.ShouldHaveSingleItem().Value.ShouldBe(42m);
        summary.OperatorActive.ShouldBeFalse();
        summary.TotalOutValue.ShouldBe(42m);
        var balanceRow = balance.Operators.ShouldHaveSingleItem();
        balanceRow.OperatorActive.ShouldBeFalse();
        balanceRow.PresentDays.ShouldBe(1);
        rollUp.Operators.Single(row => row.OperatorId == op.Id).OperatorActive.ShouldBeFalse();

        // An active entity reports true; missing and cross-branch ids stay 404.
        var activeAccount = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        (await Get<ResponseDailyLedgerJson>($"/report/daily-ledger?accountId={activeAccount.Id}&{window}", token)).AccountActive.ShouldBeTrue();
        var (_, otherBranch, _, _) = await factory.SeedFullBranchContextAsync("M11HistoricalEntitiesOther");
        var foreignAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.BankAccount, active: false);
        var foreignOperator = await factory.SeedOperatorAsync(otherBranch.Id);
        foreach (var url in new[]
                 {
                     $"/report/daily-ledger?accountId={foreignAccount.Id}&{window}",
                     $"/report/daily-ledger?accountId={Guid.NewGuid()}&{window}",
                     $"/report/operator-summary?operatorId={foreignOperator.Id}&{window}",
                     $"/report/timeentry-balance?operatorId={foreignOperator.Id}&{window}"
                 })
        {
            (await _client.GetAuthAsync(url, token)).StatusCode.ShouldBe(HttpStatusCode.NotFound, url);
        }
    }

    [Fact]
    public async Task DrillDowns_ShouldKeepMemberScopeUnchanged()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("M11HistoricalMember", Role.Member);
        var own = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(own.Id, terminal.Id, isPrimary: true);
        var retired = await factory.SeedOperatorAsync(branch.Id, "Outro, desativado");
        await DeactivateOperatorAsync(retired.Id);
        var window = $"dateFrom={_today.AddDays(-7):yyyy-MM-dd}&dateTo={_today:yyyy-MM-dd}";

        var otherSummary = await _client.GetAuthAsync($"/report/operator-summary?operatorId={retired.Id}&{window}", token);
        var ownSummary = await Get<ResponseOperatorTransactionSummaryJson>($"/report/operator-summary?mine=true&{window}", token);
        var ledger = await _client.GetAuthAsync($"/report/daily-ledger?accountId={terminal.Id}&{window}", token);

        otherSummary.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await otherSummary.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.REPORT_MEMBER_NOT_OWN_OPERATOR);
        ownSummary.OperatorId.ShouldBe(own.Id);
        ownSummary.OperatorActive.ShouldBeTrue();
        ledger.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<ResponseMonthlyReconciliationJson> Monthly(string token, int year, int month)
    {
        return await Get<ResponseMonthlyReconciliationJson>($"/report/monthly-reconciliation/{year}/{month}", token);
    }

    private async Task<T> Get<T>(string url, string token)
        where T : class
    {
        var response = await _client.GetAuthAsync(url, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<T>();
    }

    private async Task DeactivateOperatorAsync(Guid operatorId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Operators.Where(op => op.Id == operatorId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(op => op.Active, false));
    }
}
