using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerFiadoAgingHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FiadoAging_ShouldReturn200WithBucketBoundaryRows_WhenManagerRequests()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FAHappy1");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Fiado", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, "Fiado");
        var op = await factory.SeedOperatorAsync(branch.Id, "Op1");
        var client = await factory.SeedClientAsync(branch.Id, "Alice");

        var asOfDate = new DateTime(2025, 6, 30);

        // Row due exactly 30 days ago => Days0To30
        var due30 = asOfDate.AddDays(-30);
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 5, 1), value: 100m, clientId: client.Id,
            dueDate: due30);

        // Row due exactly 31 days ago => Days31To60
        var due31 = asOfDate.AddDays(-31);
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 5, 2), value: 200m, clientId: client.Id,
            dueDate: due31);

        // Row due 7 days in the future => Current
        var dueFuture = asOfDate.AddDays(7);
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 25), value: 50m, clientId: client.Id,
            dueDate: dueFuture);

        var url = $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoAgingJson>();
        payload.AsOfDate.Date.ShouldBe(asOfDate.Date);
        payload.TotalCount.ShouldBe(3);
        payload.Items.Count.ShouldBe(3);

        var item30 = payload.Items.FirstOrDefault(i => i.DueDate.Date == due30.Date);
        item30.ShouldNotBeNull();
        item30.Bucket.ShouldBe(AgingBucket.Days0To30);
        item30.DaysOutstanding.ShouldBe(30);
        item30.Value.ShouldBe(100m);
        item30.ClientId.ShouldBe(client.Id);
        item30.ClientName.ShouldBe("Alice");
        item30.AccountId.ShouldBe(tabAccount.Id);

        var item31 = payload.Items.FirstOrDefault(i => i.DueDate.Date == due31.Date);
        item31.ShouldNotBeNull();
        item31.Bucket.ShouldBe(AgingBucket.Days31To60);
        item31.DaysOutstanding.ShouldBe(31);
        item31.Value.ShouldBe(200m);

        var itemFuture = payload.Items.FirstOrDefault(i => i.DueDate.Date == dueFuture.Date);
        itemFuture.ShouldNotBeNull();
        itemFuture.Bucket.ShouldBe(AgingBucket.Current);
        itemFuture.DaysOutstanding.ShouldBe(0);
        itemFuture.Value.ShouldBe(50m);
    }

    [Fact]
    public async Task FiadoAging_ShouldExcludePaidRows_WhenPaidAtIsSet()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FAHappy2");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Bob");

        var asOfDate = new DateTime(2025, 6, 30);

        // Unpaid row — must appear
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-5));

        // Paid row — must be excluded
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 999m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-10),
            paidAt: new DateTime(2025, 6, 20));

        var url = $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoAgingJson>();
        payload.TotalCount.ShouldBe(1);
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].Value.ShouldBe(100m);
    }

    [Fact]
    public async Task FiadoAging_ShouldExcludeCancelledAndDraftRows()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FAHappy3");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Carol");

        var asOfDate = new DateTime(2025, 6, 30);
        var dueDate = asOfDate.AddDays(-10);

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: client.Id,
            dueDate: dueDate, status: TransactionStatus.Active);

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 999m, clientId: client.Id,
            dueDate: dueDate, status: TransactionStatus.Draft);

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 3), value: 999m, clientId: client.Id,
            dueDate: dueDate, status: TransactionStatus.Cancelled);

        var url = $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoAgingJson>();
        payload.TotalCount.ShouldBe(1);
        payload.Items[0].Value.ShouldBe(100m);
    }

    [Fact]
    public async Task FiadoAging_ShouldExcludeNonTabAccountRows()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FAHappy4");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa");
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Dave");

        var asOfDate = new DateTime(2025, 6, 30);
        var dueDate = asOfDate.AddDays(-5);

        // Tab row — must appear
        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: client.Id,
            dueDate: dueDate);

        // Terminal row — must not appear
        await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 999m, clientId: client.Id,
            dueDate: dueDate);

        var url = $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoAgingJson>();
        payload.TotalCount.ShouldBe(1);
        payload.Items[0].AccountId.ShouldBe(tabAccount.Id);
    }

    [Fact]
    public async Task FiadoAging_ShouldIsolatePerBranch_WhenAnotherBranchHasActivity()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FAHappy5");
        var otherBranch = await factory.SeedBranchForOtherContextAsync("FAHappy5-other");

        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Eve");

        var asOfDate = new DateTime(2025, 6, 30);

        await factory.SeedTransactionAsync(
            branch.Id, tabAccount.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 75m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-10));

        // Other branch row — must not appear
        var otherTab = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Tab);
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id);
        var otherType = await factory.SeedTransactionTypeAsync(otherCategory.Id);
        var otherOp = await factory.SeedOperatorAsync(otherBranch.Id);
        var otherClient = await factory.SeedClientAsync(otherBranch.Id, "Hidden");

        await factory.SeedTransactionAsync(
            otherBranch.Id, otherTab.Id, otherType.Id, otherCategory.Id,
            Direction.Out, otherOp.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 999m, clientId: otherClient.Id,
            dueDate: asOfDate.AddDays(-10));

        var url = $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoAgingJson>();
        payload.TotalCount.ShouldBe(1);
        payload.Items[0].Value.ShouldBe(75m);
    }

    [Fact]
    public async Task FiadoAging_ShouldReturnCorrectPagination_WhenMultiplePages()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FAHappy6");
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id);

        var asOfDate = new DateTime(2025, 6, 30);

        for (var i = 1; i <= 7; i++)
        {
            await factory.SeedTransactionAsync(
                branch.Id, tabAccount.Id, transactionType.Id, category.Id,
                Direction.Out, op.Id, user.Id,
                date: new DateTime(2025, 6, 1), value: i * 10m, clientId: client.Id,
                dueDate: asOfDate.AddDays(-i));
        }

        // Page 1
        var url1 = $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=3";
        var r1 = await _client.GetAuthAsync(url1, token);
        r1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var p1 = await r1.ReadContentAsync<ResponseFiadoAgingJson>();
        p1.TotalCount.ShouldBe(7);
        p1.TotalPages.ShouldBe(3);
        p1.Items.Count.ShouldBe(3);
        p1.HasPrevious.ShouldBeFalse();
        p1.HasNext.ShouldBeTrue();

        // Page 3
        var url3 = $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=3&pageSize=3";
        var r3 = await _client.GetAuthAsync(url3, token);
        r3.StatusCode.ShouldBe(HttpStatusCode.OK);
        var p3 = await r3.ReadContentAsync<ResponseFiadoAgingJson>();
        p3.TotalCount.ShouldBe(7);
        p3.Items.Count.ShouldBe(1);
        p3.HasPrevious.ShouldBeTrue();
        p3.HasNext.ShouldBeFalse();
    }

    [Fact]
    public async Task FiadoAging_ShouldReturn200_WhenAdminRequests()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("FAHappy7", Role.Admin);
        var url = "/report/fiado/aging?page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FiadoAging_ShouldFilterByAccountId_WhenAccountIdProvided()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FAHappy8");
        var tab1 = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Tab1");
        var tab2 = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Tab2");
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id);

        var asOfDate = new DateTime(2025, 6, 30);
        var dueDate = asOfDate.AddDays(-5);

        await factory.SeedTransactionAsync(
            branch.Id, tab1.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m, clientId: client.Id, dueDate: dueDate);

        await factory.SeedTransactionAsync(
            branch.Id, tab2.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 999m, clientId: client.Id, dueDate: dueDate);

        var url = $"/report/fiado/aging?accountId={tab1.Id}&asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseFiadoAgingJson>();
        payload.TotalCount.ShouldBe(1);
        payload.Items[0].AccountId.ShouldBe(tab1.Id);
    }
}
