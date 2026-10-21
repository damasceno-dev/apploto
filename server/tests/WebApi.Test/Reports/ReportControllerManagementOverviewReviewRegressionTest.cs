using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
using static WebApi.Test.Reports.ManagementOverviewFixture;

namespace WebApi.Test.Reports;

/// <summary>
/// Regressions for the M11 Phases 4–7 review: a huge but valid page never overflows into an error; every query the
/// overview runs inside its snapshot receives the request's cancellation token (a stall at each previously
/// uncancellable stage observes the cancellation); and whole-set counts stay exact while every preview is limited in
/// the database, with the detail-only reads never executed by the overview.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerManagementOverviewReviewRegressionTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HugeValidPage_ShouldReturnAnEmptyPage_WithWholeFilterTotals_NotAnError()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewHugePage");
        var page = $"Page={int.MaxValue}&PageSize=200";

        var upcoming = await _client.GetAuthAsync(
            $"/report/upcoming-settlements?DueDateFrom={T:yyyy-MM-dd}&DueDateTo={T.AddDays(13):yyyy-MM-dd}&{page}", f.ManagerToken);
        upcoming.StatusCode.ShouldBe(HttpStatusCode.OK, await upcoming.Content.ReadAsStringAsync());
        var settlements = await upcoming.ReadContentAsync<ResponseUpcomingSettlementsJson>();
        settlements.Items.ShouldBeEmpty();
        (settlements.TotalCount, settlements.TotalValue, settlements.TotalPages, settlements.HasPrevious, settlements.HasNext)
            .ShouldBe((3, 150m, 1, true, false));

        var cheques = await _client.GetAuthAsync($"/report/cheques/open-aging?{page}", f.ManagerToken);
        cheques.StatusCode.ShouldBe(HttpStatusCode.OK, await cheques.Content.ReadAsStringAsync());
        var aging = await cheques.ReadContentAsync<ResponseOpenChequeAgingJson>();
        aging.Items.ShouldBeEmpty();
        (aging.TotalCount, aging.OutstandingTotal, aging.HasPrevious, aging.HasNext).ShouldBe((4, 700m, true, false));

        // An empty branch with nothing to page: still an empty 200.
        var (_, _, _, emptyToken) = await factory.SeedFullBranchContextAsync("OverviewHugePageEmpty", Role.Manager);
        (await _client.GetAuthAsync(
                $"/report/upcoming-settlements?DueDateFrom={T:yyyy-MM-dd}&DueDateTo={T:yyyy-MM-dd}&{page}", emptyToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EveryQueryInsideTheSnapshot_ShouldReceiveTheRequestToken_AndNoDetailOnlyReadShouldRun()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewTokenRecording");
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();

        IReadOnlyList<RecordedCommand> recorded;
        using (var recording = interceptor.Record())
        {
            (await _client.GetAuthAsync("/report/management-overview", f.ManagerToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
            recorded = recording.Stop();
        }

        var snapshotStart = recorded.ToList().FindIndex(command => command.Sql.Contains("SET TRANSACTION READ ONLY"));
        snapshotStart.ShouldBeGreaterThanOrEqualTo(0);
        var insideSnapshot = recorded.Skip(snapshotStart + 1).ToList();
        insideSnapshot.Count.ShouldBeGreaterThan(20);
        insideSnapshot.Where(command => command.CanBeCanceled is false).Select(command => command.Sql).ShouldBeEmpty();

        // Totals and previews come from database aggregates: the overview never runs the detailed monthly role
        // grouping (per recording operator) or the unbounded per-client fiado balance list.
        insideSnapshot.ShouldNotContain(command =>
            command.Sql.Contains("\"MonthlyFinancialRole\"") && command.Sql.Contains("\"RecordedByOperatorId\""));
        insideSnapshot.ShouldNotContain(command => command.Sql.Contains("ORDER BY b.\"ClientName\", b.\"ClientId\""));
    }

    [Theory]
    [InlineData("FROM \"TimeEntries\"")]
    [InlineData("FROM \"Operators\"")]
    [InlineData("\"OperatorLinkRequests\"")]
    public async Task CancellingWhileAStageRuns_ShouldReachThatStagesQuery(string stageMarker)
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var f = await SeedAsync(factory, _client, "OverviewStageCancellation");
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stageTokenCancelable = false;
        var stageSawCancellation = false;
        using var cts = new CancellationTokenSource();

        using (interceptor.Arm(
                   command => command.CommandText.Contains(stageMarker, StringComparison.Ordinal),
                   async stageToken =>
                   {
                       stageTokenCancelable = stageToken.CanBeCanceled;
                       reached.SetResult();
                       await release.Task;
                       stageSawCancellation = stageToken.IsCancellationRequested;
                   }))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/report/management-overview");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", f.ManagerToken);
            var call = _client.SendAsync(request, cts.Token);
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cts.CancelAsync();
            release.SetResult();

            await Should.ThrowAsync<OperationCanceledException>(() => call);
        }

        stageTokenCancelable.ShouldBeTrue();
        stageSawCancellation.ShouldBeTrue();
        (await _client.GetAuthAsync("/report/management-overview", f.ManagerToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WholeSetCounts_ShouldStayExact_WhilePreviewsAreLimited()
    {
        using var clock = factory.FreezeBranchLocalTime(Now);
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OverviewCaps", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var variance = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Tab");
        var saidas = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var fiado = await factory.SeedTransactionTypeAsync(saidas.Id, "Cliente", requiresTabAccountAndClient: true);
        var terminals = new List<Account>();
        for (var i = 0; i < 52; i++)
            terminals.Add(await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, $"Terminal {i:00}"));
        var operators = new List<Operator>();
        for (var i = 0; i < 52; i++)
            operators.Add(await factory.SeedOperatorAsync(branch.Id, $"Operador {i:00}"));
        for (var i = 0; i < 7; i++)
        {
            var client = await factory.SeedClientAsync(branch.Id, $"Cliente {i}");
            await factory.SeedTransactionAsync(branch.Id, tab.Id, fiado.Id, saidas.Id, Direction.Out, operators[0].Id, user.Id,
                date: T.AddDays(-3), value: 10m * (i + 1), clientId: client.Id);
            var close = await factory.SeedDailyCloseAsync(branch.Id, terminals[i].Id, T.AddDays(-2), DailyCloseStatus.Approved,
                submittedByOperatorId: operators[i].Id);
            await factory.SeedDailyCloseItemAsync(close.Id, variance.Id, i % 2 == 0 ? i : -i);
        }
        // Operator 51 recorded rows today: it sorts first by row count, ahead of the name order.
        await factory.SeedTransactionAsync(branch.Id, terminals[51].Id, fiado.Id, saidas.Id, Direction.Out, operators[51].Id, user.Id,
            date: T, value: 1m, clientId: null);

        var overview = await (await _client.GetAuthAsync("/report/management-overview", token))
            .ReadContentAsync<ResponseManagementOverviewJson>();

        overview.AccountActivity.TotalCount.ShouldBe(52);
        overview.AccountActivity.Items.Count.ShouldBe(50);
        overview.AccountActivity.Items.Select(item => item.AccountName)
            .ShouldBe(Enumerable.Range(0, 50).Select(i => $"Terminal {i:00}"));
        overview.OperatorActivity.TotalCount.ShouldBe(52);
        overview.OperatorActivity.Items.Count.ShouldBe(50);
        overview.OperatorActivity.Items[0].OperatorId.ShouldBe(operators[51].Id);
        overview.OperatorActivity.Items.Skip(1).Select(item => item.OperatorName)
            .ShouldBe(Enumerable.Range(0, 49).Select(i => $"Operador {i:00}"));
        overview.Fiado.TotalCount.ShouldBe(7);
        overview.Fiado.DebtorCount.ShouldBe(7);
        overview.Fiado.TotalDebt.ShouldBe(280m);
        overview.Fiado.TopDebtors.Select(item => item.OutstandingTotal).ShouldBe([70m, 60m, 50m, 40m, 30m]);
        overview.CashVariance.TotalCount.ShouldBe(7);
        overview.CashVariance.TotalVariance.ShouldBe(0m + 2m + 4m + 6m - 1m - 3m - 5m);
        overview.CashVariance.LargestVariances.Select(item => item.VarianceValue).ShouldBe([6m, -5m, 4m, -3m, 2m]);
    }
}
