using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;
using static WebApi.Test.Reports.ManagementOverviewFixture;

namespace WebApi.Test.Reports;

/// <summary>
/// M11 Phase 7 acceptance scenarios A13–A21: month selector, empty and unconfigured branches, access, never-commits,
/// one snapshot under a concurrent committed write, cancellation and atomic failure, determinism and time advance.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerManagementOverviewBehaviorTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<ResponseManagementOverviewJson> Overview(string token, string query = "")
    {
        var response = await _client.GetAuthAsync($"/report/management-overview{query}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseManagementOverviewJson>();
    }

    private async Task<List<string>> ErrorsOf(string token, string query)
    {
        var response = await _client.GetAuthAsync($"/report/management-overview{query}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        return (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages;
    }

    private async Task<(string Token, Branch Branch, User User)> BareBranchAsync(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        return (token, branch, user);
    }

    [Fact]
    public async Task A13_MonthSelector_ShouldDefaultToTheCurrentPartialMonth_AcceptAPastMonth_AndRefuseInvalidOnes()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var (token, _, _) = await BareBranchAsync("OverviewSelector");

        var current = await Overview(token);
        (current.Year, current.Month, current.IsPartialMonth, current.MonthDateFrom, current.MonthDateTo)
            .ShouldBe((2026, 10, true, new DateTime(2026, 10, 1), T));

        var previous = await Overview(token, "?Year=2026&Month=9");
        (previous.IsPartialMonth, previous.MonthDateFrom, previous.MonthDateTo)
            .ShouldBe((false, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)));
        previous.AsOfDate.ShouldBe(T);

        (await ErrorsOf(token, "?Year=2026&Month=11")).ShouldBe([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);
        (await ErrorsOf(token, "?Year=2026")).ShouldBe([ResourcesErrorMessages.REPORT_YEAR_MONTH_PAIR_REQUIRED]);
        (await ErrorsOf(token, "?Month=9")).ShouldBe([ResourcesErrorMessages.REPORT_YEAR_MONTH_PAIR_REQUIRED]);
        (await ErrorsOf(token, "?Year=2026&Month=13")).ShouldBe([ResourcesErrorMessages.REPORT_MONTH_INVALID]);
        (await ErrorsOf(token, "?Year=1999&Month=12")).ShouldBe([ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE]);
    }

    [Fact]
    public async Task A14_EmptyBranch_ShouldReportEverySectionEmptyOrNotConfigured_NeverReadyWithZeros()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);

        // A freshly onboarded branch: the seed classifies its card/cheque and bolão types, so nothing is unconfigured.
        var seeded = await factory.SeedConfiguredBranchAsync($"OverviewSeeded {Guid.NewGuid():N}");
        var user = await factory.SeedUserAsync();
        var seededToken = factory.IssueBranchToken(await factory.SeedBranchUserAsync(user.Id, seeded.Id, Role.Manager));
        var fresh = await Overview(seededToken);
        Statuses(fresh).ShouldAllBe(status => status == OverviewSectionStatus.Empty);

        // A branch with no classified type and no report marker.
        var (bareToken, _, _) = await BareBranchAsync("OverviewBare");
        var bare = await Overview(bareToken);
        bare.UpcomingSettlements.Status.ShouldBe(OverviewSectionStatus.NotConfigured);
        bare.MonthlyFinancial.Status.ShouldBe(OverviewSectionStatus.NotConfigured);
        bare.MonthlyFinancial.BolaoServiceFeeTotal.ShouldBeNull();
        bare.MonthlyFinancial.CommercialLeftoverTotal.ShouldBeNull();
        Statuses(bare).ShouldNotContain(OverviewSectionStatus.Ready);
    }

    private static OverviewSectionStatus[] Statuses(ResponseManagementOverviewJson overview) =>
    [
        overview.Queue.Status, overview.RecentCloses.Status, overview.CashVariance.Status, overview.Reconciliation.Status,
        overview.Fiado.Status, overview.OpenCheques.Status, overview.UpcomingSettlements.Status, overview.AccountActivity.Status,
        overview.OperatorActivity.Status, overview.Hours.Status, overview.MonthlyFinancial.Status
    ];

    [Fact]
    public async Task A15_Access_ShouldAdmitManagerAndAdminWithoutAnOperator_AndRefuseOthers()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var (managerToken, branch, _) = await BareBranchAsync("OverviewAccess");
        var admin = await factory.SeedUserAsync();
        var adminToken = factory.IssueBranchToken(await factory.SeedBranchUserAsync(admin.Id, branch.Id, Role.Admin));
        var member = await factory.SeedUserAsync();
        var memberToken = factory.IssueBranchToken(await factory.SeedBranchUserAsync(member.Id, branch.Id, Role.Member));

        (await _client.GetAsync("/report/management-overview")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.GetAuthAsync("/report/management-overview", memberToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _client.GetAuthAsync("/report/management-overview", managerToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAuthAsync("/report/management-overview", adminToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A16_Overview_ShouldLeaveTheDatabaseUnchanged_AndItsSnapshotShouldRefuseWrites()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewNeverCommits");
        var before = await DatabaseFingerprintAsync();

        await Overview(f.ManagerToken);

        (await DatabaseFingerprintAsync()).ShouldBe(before);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await using (await scope.ServiceProvider.GetRequiredService<IReportReadSnapshot>().Begin())
        {
            var write = await Should.ThrowAsync<PostgresException>(() =>
                db.Database.ExecuteSqlRawAsync("UPDATE \"Settings\" SET \"DailyTargetHours\" = \"DailyTargetHours\""));
            write.SqlState.ShouldBe(PostgresErrorCodes.ReadOnlySqlTransaction);
        }
    }

    private async Task<string> DatabaseFingerprintAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var parts = new List<string>();
        foreach (var table in new[] { "Transactions", "DailyCloses", "DailyCloseItems", "TimeEntries", "TimeEntrySegments", "Settings",
                     "TransactionTypes", "IdempotencyRequests" })
        {
            var rows = await db.Database
                .SqlQueryRaw<string>($"SELECT count(*)::text || ':' || coalesce(sum(xmin::text::bigint), 0)::text AS \"Value\" FROM \"{table}\"")
                .ToListAsync();
            parts.Add($"{table}={rows.Single()}");
        }

        return string.Join(';', parts);
    }

    [Fact]
    public async Task A17_AWriteCommittedBetweenSections_ShouldBeInvisibleToTheLaterSections()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewSnapshot");
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        var committed = false;

        // After the queue section's first close query returns — the snapshot already taken — another connection commits
        // a 1,000 inflow on T1 for today by O1. Account and operator activity run later and must not see it.
        using (interceptor.Arm(
                   command => command.CommandText.Contains("FROM \"DailyCloses\"", StringComparison.Ordinal),
                   async _ =>
                   {
                       await using var other = new ServerDbContext(new DbContextOptionsBuilder<ServerDbContext>()
                           .UseNpgsql(factory.DisposableConnectionString).Options);
                       var category = await other.Categories.AsNoTracking().FirstAsync(c => c.BranchId == f.Branch.Id && c.Name == "Entradas");
                       var venda = await other.TransactionTypes.AsNoTracking().FirstAsync(t => t.CategoryId == category.Id && t.Name == "Venda");
                       // A cashbox row always belongs to its session (M9.6 Phase 2): today's T1 custody.
                       var session = await other.DailyCloses.AsNoTracking()
                           .Where(close => close.AccountId == f.T1.Id && close.Date == T && close.MappingState == SessionMappingState.Ready)
                           .Select(close => close.Id)
                           .FirstAsync();
                       other.Transactions.Add(new Transaction
                       {
                           SessionId = session,
                           BranchId = f.Branch.Id, AccountId = f.T1.Id, TransactionTypeId = venda.Id, CategoryId = category.Id,
                           Direction = Direction.In, Date = T, DueDate = T, Value = 1000m, RecordedByOperatorId = f.O1.Id,
                           CreatedByUserId = (await other.Users.AsNoTracking().FirstAsync()).Id, Description = "concurrent write"
                       });
                       await other.SaveChangesAsync();
                       committed = true;
                   }))
        {
            var during = await Overview(f.ManagerToken);
            committed.ShouldBeTrue();
            during.AccountActivity.Items.Single(item => item.AccountId == f.T1.Id).TotalIn.ShouldBe(140m);
            during.OperatorActivity.Items.Single(item => item.OperatorId == f.O1.Id).TransactionCount.ShouldBe(3);
        }

        var after = await Overview(f.ManagerToken);
        after.AccountActivity.Items.Single(item => item.AccountId == f.T1.Id).TotalIn.ShouldBe(1140m);
        after.OperatorActivity.Items.Single(item => item.OperatorId == f.O1.Id).TransactionCount.ShouldBe(4);
    }

    [Fact]
    public async Task A18_ACancelledRequest_ShouldEndWithoutAResponse_AndAMidwayFailureShouldFailTheWholeRequest()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewCancellation");
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverSawCancellation = false;
        using var cts = new CancellationTokenSource();

        using (interceptor.Arm(
                   command => command.CommandText.Contains("FROM \"DailyCloses\"", StringComparison.Ordinal),
                   async requestToken =>
                   {
                       reached.SetResult();
                       await release.Task;
                       serverSawCancellation = requestToken.IsCancellationRequested;
                   }))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/report/management-overview");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", f.ManagerToken);
            var call = _client.SendAsync(request, cts.Token);
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cts.CancelAsync();
            release.SetResult();

            await Should.ThrowAsync<OperationCanceledException>(() => call);
        }

        serverSawCancellation.ShouldBeTrue();

        // A runtime failure between sections fails the whole request: an error envelope, never a partial 200.
        using (interceptor.Arm(
                   command => command.CommandText.Contains("\"TimeEntries\"", StringComparison.Ordinal),
                   _ => throw new InvalidOperationException("injected section failure")))
        {
            var failed = await _client.GetAuthAsync("/report/management-overview", f.ManagerToken);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await failed.Content.ReadAsStringAsync()).ShouldNotContain("accountActivity");
        }

        // The snapshot was released: the next request reads normally.
        (await Overview(f.ManagerToken)).AccountActivity.Items.Single(item => item.AccountId == f.T1.Id).TotalIn.ShouldBe(140m);
    }

    [Fact]
    public async Task A20_SameDataAndFrozenInstant_ShouldReturnIdenticalBodies()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewDeterminism");

        var first = await (await _client.GetAuthAsync("/report/management-overview", f.ManagerToken)).Content.ReadAsStringAsync();
        var second = await (await _client.GetAuthAsync("/report/management-overview", f.ManagerToken)).Content.ReadAsStringAsync();

        second.ShouldBe(first);
        JsonNode.Parse(first)!["branchLocalNow"]!.GetValue<DateTime>().ShouldBe(Now);
    }

    [Theory]
    // Within one tier: clock-in 08:00, 09:00 → 10:00: 1.00 h → 2.00 h, balance −6.33 → −5.33 (target 7.33 h).
    [InlineData(8, 9, 10, 1.00, -6.33, 2.00, -5.33)]
    // Across a tier: clock-in 09:00, 15:00 → 16:00: gross 6 → 7 h moves the deduction 0.25 → 1.00.
    [InlineData(9, 15, 16, 5.75, -1.58, 6.00, -1.33)]
    public async Task A21_AdvancingTheClock_ShouldMoveOnlyTheInstantAndTodaysLiveHours(
        int clockIn, int firstHour, int secondHour, double firstHours, double firstBalance, double secondHours, double secondBalance)
    {
        using var clock = factory.FreezeBranchLocalTime(T.AddHours(firstHour));
        var (token, branch, _) = await BareBranchAsync($"OverviewTimeAdvance{clockIn}");
        var op = await factory.SeedOperatorAsync(branch.Id, "Live operator");
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, op.Id, T, TimeEntryStatus.Present,
            (T.AddHours(clockIn), null, true));

        var first = JsonNode.Parse(await (await _client.GetAuthAsync("/report/management-overview", token)).Content.ReadAsStringAsync())!;
        clock.AdvanceTo(T.AddHours(secondHour));
        var second = JsonNode.Parse(await (await _client.GetAuthAsync("/report/management-overview", token)).Content.ReadAsStringAsync())!;

        AssertHours(first, (decimal)firstHours, (decimal)firstBalance);
        AssertHours(second, (decimal)secondHours, (decimal)secondBalance);
        first["hours"]!["liveNowCount"]!.GetValue<int>().ShouldBe(1);
        second["hours"]!["liveNowCount"]!.GetValue<int>().ShouldBe(1);

        // Everything else — every other section, window and count — is byte-for-byte the same.
        foreach (var node in new[] { first, second })
        {
            node.AsObject().Remove("generatedAtUtc");
            node.AsObject().Remove("branchLocalNow");
            var hours = node["hours"]!.AsObject();
            hours.Remove("negativeBalanceHours");
            foreach (var row in hours["mostBehind"]!.AsArray())
            {
                row!.AsObject().Remove("totalHours");
                row.AsObject().Remove("totalBalanceHours");
            }
            node["monthlyFinancial"]!.AsObject().Remove("hoursTotal");
        }
        second.ToJsonString().ShouldBe(first.ToJsonString());
    }

    private static void AssertHours(JsonNode overview, decimal totalHours, decimal balance)
    {
        var behind = overview["hours"]!["mostBehind"]![0]!;
        decimal.Round(behind["totalHours"]!.GetValue<decimal>(), 2).ShouldBe(totalHours);
        decimal.Round(behind["totalBalanceHours"]!.GetValue<decimal>(), 2).ShouldBe(balance);
        decimal.Round(overview["monthlyFinancial"]!["hoursTotal"]!.GetValue<decimal>(), 2).ShouldBe(totalHours);
    }

    [Fact]
    public async Task A21_CrossingBranchLocalMidnight_ShouldMoveTheOpenSegmentFromLiveToUnresolved()
    {
        using var clock = factory.FreezeBranchLocalTime(T.AddHours(23).AddMinutes(30));
        var (token, branch, _) = await BareBranchAsync("OverviewMidnight");
        var op = await factory.SeedOperatorAsync(branch.Id, "Night operator");
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(factory, branch.Id, op.Id, T, TimeEntryStatus.Present,
            (T.AddHours(22), null, true));

        var beforeMidnight = await Overview(token);
        beforeMidnight.AsOfDate.ShouldBe(T);
        (beforeMidnight.Hours.LiveNowCount, beforeMidnight.Hours.UnresolvedOpenCount).ShouldBe((1, 0));
        beforeMidnight.Hours.MostBehind.Single().TotalHours.ShouldBe(1.5m);

        clock.AdvanceTo(T.AddDays(1).AddMinutes(30));
        var afterMidnight = await Overview(token);
        afterMidnight.AsOfDate.ShouldBe(T.AddDays(1));
        afterMidnight.MonthDateTo.ShouldBe(T.AddDays(1));
        afterMidnight.AccountActivity.Date.ShouldBe(T.AddDays(1));
        afterMidnight.RecentCloses.DateTo.ShouldBe(T.AddDays(1));
        (afterMidnight.Hours.LiveNowCount, afterMidnight.Hours.UnresolvedOpenCount).ShouldBe((0, 1));
        afterMidnight.Hours.ContainsInProgress.ShouldBeTrue();
        var stale = afterMidnight.Hours.MostBehind.Single();
        stale.TotalHours.ShouldBe(0m);
        stale.ContainsInProgress.ShouldBeTrue();
    }
}
