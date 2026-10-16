using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M7.7 Phase 7.5 (design gap W): the Work Queue backlog aggregate and the unlocked Draft-transaction
/// count on <c>GET /report/manager-queue</c>. The two facts share the LockDate floor but stop on different
/// days — the backlog before the requested date, the Draft count at branch-local today.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerManagerQueueBacklogHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // Backlog: per-status counts, oldest date, and every exclusion in one branch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ManagerQueue_ShouldCountPendingClosesPerStatus_WhenUnlockedDaysBeforeRequestedDateHoldThem()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashBacklog1", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        await factory.SeedSettingAsync(branch.Id, lockDate: new DateTime(2025, 1, 31));
        var op = await factory.SeedOperatorAsync(branch.Id, "Backlog Operator");
        var accountA = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Backlog A");
        var accountB = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Backlog B");
        var accountC = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Backlog C");

        // Counted: LockDate < Date < requested date, Submitted/Rejected/Draft.
        await SeedCloseAsync(branch.Id, accountA.Id, op.Id, new DateTime(2025, 2, 3), DailyCloseStatus.Submitted);
        await SeedCloseAsync(branch.Id, accountB.Id, op.Id, new DateTime(2025, 2, 10), DailyCloseStatus.Submitted);
        await SeedCloseAsync(branch.Id, accountA.Id, op.Id, new DateTime(2025, 2, 5), DailyCloseStatus.Rejected);
        await SeedCloseAsync(branch.Id, accountC.Id, op.Id, new DateTime(2025, 2, 4), DailyCloseStatus.Rejected);
        // The day immediately before the requested date is the last one inside the window.
        await SeedCloseAsync(branch.Id, accountB.Id, op.Id, new DateTime(2025, 2, 19), DailyCloseStatus.Rejected);
        await SeedCloseAsync(branch.Id, accountA.Id, op.Id, new DateTime(2025, 2, 2), DailyCloseStatus.Draft);

        // Not counted. Each of the first three sits on an earlier day than the oldest counted close,
        // so a leak would also move OldestPendingDate.
        await SeedCloseAsync(branch.Id, accountB.Id, op.Id, new DateTime(2025, 2, 1), DailyCloseStatus.Approved);
        await SeedCloseAsync(branch.Id, accountC.Id, op.Id, new DateTime(2025, 2, 1), DailyCloseStatus.Submitted, active: false);
        await SeedCloseAsync(branch.Id, accountA.Id, op.Id, new DateTime(2025, 1, 31), DailyCloseStatus.Submitted);
        await SeedCloseAsync(branch.Id, accountA.Id, op.Id, new DateTime(2025, 1, 15), DailyCloseStatus.Draft);
        await SeedCloseAsync(branch.Id, accountA.Id, op.Id, new DateTime(2025, 2, 20), DailyCloseStatus.Submitted);
        await SeedCloseAsync(branch.Id, accountB.Id, op.Id, new DateTime(2025, 2, 21), DailyCloseStatus.Rejected);
        await SeedCloseAsync(branch.Id, accountC.Id, op.Id, new DateTime(2025, 3, 1), DailyCloseStatus.Draft);

        var httpResponse = await _client.GetAuthAsync("/report/manager-queue?date=2025-02-20", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseManagerQueueJson>();

        payload.Backlog.SubmittedCount.ShouldBe(2);
        payload.Backlog.RejectedCount.ShouldBe(3);
        payload.Backlog.DraftCount.ShouldBe(1);
        payload.Backlog.OldestPendingDate.ShouldBe(new DateTime(2025, 2, 2));

        // Selected-day semantics are untouched: the requested day's own close is a review row, not backlog.
        payload.Date.ShouldBe(new DateTime(2025, 2, 20));
        payload.Closes.ShouldHaveSingleItem().AccountId.ShouldBe(accountA.Id);
        payload.PendingApprovalCount.ShouldBe(1);
    }

    [Fact]
    public async Task ManagerQueue_ShouldMoveBacklogUpperBoundWithRequestedDate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashBacklog2", Role.Admin);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        await factory.SeedSettingAsync(branch.Id, lockDate: new DateTime(2025, 3, 31));
        var op = await factory.SeedOperatorAsync(branch.Id, "Bound Operator");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Bound Terminal");

        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2025, 4, 1), DailyCloseStatus.Rejected);
        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2025, 4, 8), DailyCloseStatus.Submitted);
        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2025, 4, 9), DailyCloseStatus.Draft);

        // First unlocked day itself: nothing lies strictly between LockDate and the requested date.
        var onFirstUnlockedDay = await GetManagerQueueAsync("2025-04-01", token);
        onFirstUnlockedDay.Backlog.RejectedCount.ShouldBe(0);
        onFirstUnlockedDay.Backlog.OldestPendingDate.ShouldBeNull();

        var onSubmittedDay = await GetManagerQueueAsync("2025-04-08", token);
        onSubmittedDay.Backlog.RejectedCount.ShouldBe(1);
        onSubmittedDay.Backlog.SubmittedCount.ShouldBe(0);
        onSubmittedDay.Backlog.DraftCount.ShouldBe(0);
        onSubmittedDay.Backlog.OldestPendingDate.ShouldBe(new DateTime(2025, 4, 1));

        var afterEverything = await GetManagerQueueAsync("2025-04-10", token);
        afterEverything.Backlog.RejectedCount.ShouldBe(1);
        afterEverything.Backlog.SubmittedCount.ShouldBe(1);
        afterEverything.Backlog.DraftCount.ShouldBe(1);
        afterEverything.Backlog.OldestPendingDate.ShouldBe(new DateTime(2025, 4, 1));

        // A requested date inside the locked range has no unlocked day before it.
        var insideLockedRange = await GetManagerQueueAsync("2025-03-15", token);
        insideLockedRange.Backlog.SubmittedCount.ShouldBe(0);
        insideLockedRange.Backlog.RejectedCount.ShouldBe(0);
        insideLockedRange.Backlog.DraftCount.ShouldBe(0);
        insideLockedRange.Backlog.OldestPendingDate.ShouldBeNull();
    }

    [Fact]
    public async Task ManagerQueue_ShouldReturnZeroCountsAndNullOldestDate_WhenOnlyExcludedClosesExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashBacklog3", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        await factory.SeedSettingAsync(branch.Id, lockDate: new DateTime(2025, 4, 30));
        var op = await factory.SeedOperatorAsync(branch.Id, "Empty Operator");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Empty Terminal");

        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2025, 5, 5), DailyCloseStatus.Approved);
        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2025, 4, 30), DailyCloseStatus.Rejected);
        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2025, 5, 12), DailyCloseStatus.Submitted);

        var httpResponse = await _client.GetAuthAsync("/report/manager-queue?date=2025-05-12", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());
        var backlog = json.RootElement.GetProperty("backlog");

        backlog.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ShouldBe(
            ["draftCount", "oldestPendingDate", "rejectedCount", "submittedCount"]);
        backlog.GetProperty("submittedCount").GetRawText().ShouldBe("0");
        backlog.GetProperty("rejectedCount").GetRawText().ShouldBe("0");
        backlog.GetProperty("draftCount").GetRawText().ShouldBe("0");
        // Emitted as an explicit null, never omitted.
        backlog.GetProperty("oldestPendingDate").ValueKind.ShouldBe(JsonValueKind.Null);
        json.RootElement.GetProperty("unlockedDraftTransactionCount").GetRawText().ShouldBe("0");
    }

    [Fact]
    public async Task ManagerQueue_ShouldEmitExactBacklogJson_WhenBacklogIsPopulated()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashBacklog4", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var op = await factory.SeedOperatorAsync(branch.Id, "Json Operator");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Json Terminal");

        // No Setting row: the branch has never been locked, so every earlier day is unlocked.
        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2019, 7, 4), DailyCloseStatus.Draft);
        await SeedCloseAsync(branch.Id, account.Id, op.Id, new DateTime(2025, 6, 2), DailyCloseStatus.Submitted);

        var httpResponse = await _client.GetAuthAsync("/report/manager-queue?date=2025-06-10", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());
        var backlog = json.RootElement.GetProperty("backlog");

        backlog.GetProperty("submittedCount").GetRawText().ShouldBe("1");
        backlog.GetProperty("rejectedCount").GetRawText().ShouldBe("0");
        backlog.GetProperty("draftCount").GetRawText().ShouldBe("1");
        // Same wire format as the manager queue's own Date member.
        backlog.GetProperty("oldestPendingDate").GetRawText().ShouldBe("\"2019-07-04T00:00:00\"");
        json.RootElement.GetProperty("date").GetRawText().ShouldBe("\"2025-06-10T00:00:00\"");
        json.RootElement.GetProperty("unlockedDraftTransactionCount").ValueKind.ShouldBe(JsonValueKind.Number);
    }

    // -------------------------------------------------------------------------
    // Unlocked Draft transactions: status/date predicates and the today bound
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ManagerQueue_ShouldCountDraftTransactionsThroughBranchLocalToday_WhateverDateIsRequested()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DashBacklog5", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var today = BranchLocalToday();
        var lockDate = today.AddDays(-10);
        await factory.SeedSettingAsync(branch.Id, lockDate: lockDate);
        var ledger = await SeedLedgerAsync(branch.Id, user.Id, "Draft Count");

        // Counted: Draft, active row, LockDate < Date <= today.
        await SeedTransactionAsync(ledger, lockDate.AddDays(1), TransactionStatus.Draft);
        await SeedTransactionAsync(ledger, today.AddDays(-1), TransactionStatus.Draft);
        await SeedTransactionAsync(ledger, today, TransactionStatus.Draft);

        // Not counted: locked days, the future, other statuses, soft-deleted rows.
        await SeedTransactionAsync(ledger, lockDate, TransactionStatus.Draft);
        await SeedTransactionAsync(ledger, lockDate.AddDays(-1), TransactionStatus.Draft);
        await SeedTransactionAsync(ledger, today.AddDays(1), TransactionStatus.Draft);
        await SeedTransactionAsync(ledger, today.AddDays(-1), TransactionStatus.Active);
        await SeedTransactionAsync(ledger, today.AddDays(-1), TransactionStatus.Cancelled);
        await SeedTransactionAsync(ledger, today.AddDays(-1), TransactionStatus.Draft, active: false);

        // One pending close, to show the backlog follows the requested date while the Draft count does not.
        await SeedCloseAsync(branch.Id, ledger.AccountId, ledger.OperatorId, today.AddDays(-2), DailyCloseStatus.Draft);

        var onToday = await GetManagerQueueAsync(today, token);
        onToday.UnlockedDraftTransactionCount.ShouldBe(3);
        onToday.Backlog.DraftCount.ShouldBe(1);

        var onEarlierUnlockedDay = await GetManagerQueueAsync(today.AddDays(-5), token);
        onEarlierUnlockedDay.UnlockedDraftTransactionCount.ShouldBe(3);
        onEarlierUnlockedDay.Backlog.DraftCount.ShouldBe(0);

        var onLockedDay = await GetManagerQueueAsync(lockDate.AddDays(-3), token);
        onLockedDay.UnlockedDraftTransactionCount.ShouldBe(3);

        var onFutureDay = await GetManagerQueueAsync(today.AddDays(3), token);
        onFutureDay.UnlockedDraftTransactionCount.ShouldBe(3);
        onFutureDay.Backlog.DraftCount.ShouldBe(1);
    }

    [Fact]
    public async Task ManagerQueue_ShouldCountEveryPastDraftTransaction_WhenBranchWasNeverLocked()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DashBacklog6", Role.Admin);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var today = BranchLocalToday();
        var ledger = await SeedLedgerAsync(branch.Id, user.Id, "Never Locked");

        await SeedTransactionAsync(ledger, new DateTime(2018, 1, 2), TransactionStatus.Draft);
        await SeedTransactionAsync(ledger, today, TransactionStatus.Draft);
        await SeedTransactionAsync(ledger, today.AddDays(1), TransactionStatus.Draft);

        var payload = await GetManagerQueueAsync(today, token);

        payload.UnlockedDraftTransactionCount.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Cross-branch isolation: rows and the LockDate both stay inside the branch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ManagerQueue_ShouldIgnoreOtherBranchClosesTransactionsAndLockDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DashBacklog7", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DashBacklog7-other");
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var today = BranchLocalToday();

        // The other branch is locked through yesterday; applying its LockDate here would hide this
        // branch's rows, and applying this branch's never-locked floor there would leak its rows.
        await factory.SeedSettingAsync(otherBranch.Id, lockDate: today.AddDays(-1));
        var ledger = await SeedLedgerAsync(branch.Id, user.Id, "Own");
        var otherLedger = await SeedLedgerAsync(otherBranch.Id, user.Id, "Other");

        await SeedCloseAsync(branch.Id, ledger.AccountId, ledger.OperatorId, today.AddDays(-4), DailyCloseStatus.Rejected);
        await SeedTransactionAsync(ledger, today.AddDays(-4), TransactionStatus.Draft);

        await SeedCloseAsync(otherBranch.Id, otherLedger.AccountId, otherLedger.OperatorId, today.AddDays(-6), DailyCloseStatus.Submitted);
        await SeedCloseAsync(otherBranch.Id, otherLedger.AccountId, otherLedger.OperatorId, today.AddDays(-5), DailyCloseStatus.Rejected);
        await SeedCloseAsync(otherBranch.Id, otherLedger.AccountId, otherLedger.OperatorId, today.AddDays(-3), DailyCloseStatus.Draft);
        await SeedTransactionAsync(otherLedger, today.AddDays(-2), TransactionStatus.Draft);
        await SeedTransactionAsync(otherLedger, today, TransactionStatus.Draft);

        var payload = await GetManagerQueueAsync(today, token);

        payload.Backlog.SubmittedCount.ShouldBe(0);
        payload.Backlog.RejectedCount.ShouldBe(1);
        payload.Backlog.DraftCount.ShouldBe(0);
        payload.Backlog.OldestPendingDate.ShouldBe(today.AddDays(-4));
        payload.UnlockedDraftTransactionCount.ShouldBe(1);
    }

    private DateTime BranchLocalToday()
    {
        using var scope = factory.Services.CreateScope();
        var branchClock = scope.ServiceProvider.GetRequiredService<IBranchClock>();
        return branchClock.LocalBusinessDate(branchClock.UtcNow());
    }

    private Task<ResponseManagerQueueJson> GetManagerQueueAsync(DateTime date, string token)
    {
        return GetManagerQueueAsync(date.ToString("yyyy-MM-dd"), token);
    }

    private async Task<ResponseManagerQueueJson> GetManagerQueueAsync(string date, string token)
    {
        var httpResponse = await _client.GetAuthAsync($"/report/manager-queue?date={date}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await httpResponse.ReadContentAsync<ResponseManagerQueueJson>();
    }

    private Task<DailyClose> SeedCloseAsync(
        Guid branchId,
        Guid accountId,
        Guid operatorId,
        DateTime date,
        DailyCloseStatus status,
        bool active = true)
    {
        var submitted = status != DailyCloseStatus.Draft;
        return factory.SeedDailyCloseAsync(
            branchId, accountId, date: date, status: status,
            submittedByOperatorId: submitted ? operatorId : null,
            submittedAt: submitted ? date.AddHours(18) : null,
            approvedAt: status == DailyCloseStatus.Approved ? date.AddHours(20) : null,
            active: active);
    }

    private async Task<LedgerContext> SeedLedgerAsync(Guid branchId, Guid userId, string label)
    {
        var account = await factory.SeedAccountAsync(branchId, AccountType.Terminal, $"{label} Terminal");
        var op = await factory.SeedOperatorAsync(branchId, $"{label} Operator");
        var category = await factory.SeedCategoryAsync(branchId, $"{label} Category");
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, $"{label} Type");
        return new LedgerContext(branchId, account.Id, op.Id, category.Id, transactionType.Id, userId);
    }

    private Task<Transaction> SeedTransactionAsync(
        LedgerContext ledger,
        DateTime date,
        TransactionStatus status,
        bool active = true)
    {
        return factory.SeedTransactionAsync(
            ledger.BranchId, ledger.AccountId, ledger.TransactionTypeId, ledger.CategoryId, Direction.In,
            ledger.OperatorId, ledger.UserId, date: date, status: status, active: active);
    }

    private sealed record LedgerContext(
        Guid BranchId,
        Guid AccountId,
        Guid OperatorId,
        Guid CategoryId,
        Guid TransactionTypeId,
        Guid UserId);
}
