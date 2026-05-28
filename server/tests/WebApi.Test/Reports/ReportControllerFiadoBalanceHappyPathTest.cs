using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerFiadoBalanceHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FiadoBalance_ShouldReturn200WithPerClientOutstanding_WhenManagerRequests()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FBHappy1");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Fiado", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, "Fiado");
        var op = await factory.SeedOperatorAsync(branch.Id, "Op1");
        var alpha = await factory.SeedClientAsync(branch.Id, "Alpha");
        var beta = await factory.SeedClientAsync(branch.Id, "Beta");

        // Alpha: Out 200, In 50 => 150
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 200m, clientId: alpha.Id);
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: new DateTime(2025, 6, 5), value: 50m, clientId: alpha.Id);

        // Beta: Out 75 => 75
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 10), value: 75m, clientId: beta.Id);

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.AsOfDate.Date.ShouldBe(new DateTime(2025, 6, 30).Date);
        payload.Items.Count.ShouldBe(2);
        payload.Items[0].ClientId.ShouldBe(alpha.Id);
        payload.Items[0].ClientName.ShouldBe("Alpha");
        payload.Items[0].OutstandingTotal.ShouldBe(150m);
        payload.Items[1].ClientId.ShouldBe(beta.Id);
        payload.Items[1].ClientName.ShouldBe("Beta");
        payload.Items[1].OutstandingTotal.ShouldBe(75m);
        payload.TotalOutstanding.ShouldBe(225m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldExcludeNonTabAccountRows_WhenSameBranchHasTerminalActivity()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FBHappy2");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa");
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Carlos");

        // Tab row — included
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 300m, clientId: client.Id);

        // Terminal row — must not appear in fiado balance
        await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 999m, clientId: client.Id);

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].ClientId.ShouldBe(client.Id);
        payload.Items[0].OutstandingTotal.ShouldBe(300m);
        payload.TotalOutstanding.ShouldBe(300m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldExcludeCancelledAndDraftRows_WhenWindowContainsThem()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FBHappy3");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Diana");

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: client.Id,
            status: TransactionStatus.Active);

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 999m, clientId: client.Id,
            status: TransactionStatus.Draft);

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 3), value: 999m, clientId: client.Id,
            status: TransactionStatus.Cancelled);

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].ClientId.ShouldBe(client.Id);
        payload.Items[0].OutstandingTotal.ShouldBe(100m);
        payload.TotalOutstanding.ShouldBe(100m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldExcludeZeroBalanceClients_WhenInAndOutSumToZero()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FBHappy4");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var zeroClient = await factory.SeedClientAsync(branch.Id, "Zero");
        var positiveClient = await factory.SeedClientAsync(branch.Id, "Positivo");

        // Zero balance — should not appear
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: zeroClient.Id);
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 100m, clientId: zeroClient.Id);

        // Positive balance — should appear
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 3), value: 80m, clientId: positiveClient.Id);

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].ClientId.ShouldBe(positiveClient.Id);
        payload.TotalOutstanding.ShouldBe(80m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldExcludeRowsAfterAsOfDate_WhenWindowIsBoundedByAsOfDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FBHappy5");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Boundary");

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: client.Id);

        // After asOfDate — must be excluded
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 7, 15), value: 999m, clientId: client.Id);

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].OutstandingTotal.ShouldBe(100m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldFilterToSingleClient_WhenClientIdProvided()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FBHappy6");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client1 = await factory.SeedClientAsync(branch.Id, "First");
        var client2 = await factory.SeedClientAsync(branch.Id, "Second");

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: client1.Id);

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 50m, clientId: client2.Id);

        var url = $"/report/fiado/balance?clientId={client1.Id}&asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].ClientId.ShouldBe(client1.Id);
        payload.TotalOutstanding.ShouldBe(100m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldIsolatePerBranch_WhenAnotherBranchHasFiadoActivity()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FBHappy7");
        var otherBranch = await factory.SeedBranchForOtherContextAsync("FBHappy7-other");

        // Caller's branch fiado activity
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var visibleClient = await factory.SeedClientAsync(branch.Id, "Visible");

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 75m, clientId: visibleClient.Id);

        // Another branch's fiado activity — must not leak
        var otherTab = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Tab);
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id);
        var otherTransactionType = await factory.SeedTransactionTypeAsync(otherCategory.Id);
        var otherOp = await factory.SeedOperatorAsync(otherBranch.Id);
        var otherClient = await factory.SeedClientAsync(otherBranch.Id, "Hidden");

        await factory.SeedTransactionAsync(
            otherBranch.Id, otherTab.Id, otherTransactionType.Id, otherCategory.Id,
            Direction.Out, otherOp.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 999m, clientId: otherClient.Id);

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].ClientId.ShouldBe(visibleClient.Id);
        payload.TotalOutstanding.ShouldBe(75m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldReturnEmptyEnvelope_WhenNoFiadoActivityExists()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FBHappy8");

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        payload.Items.ShouldBeEmpty();
        payload.TotalOutstanding.ShouldBe(0m);
    }

    [Fact]
    public async Task FiadoBalance_ShouldReturn200_WhenAdminRequests()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FBHappy9", Role.Admin);

        var url = "/report/fiado/balance?asOfDate=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
