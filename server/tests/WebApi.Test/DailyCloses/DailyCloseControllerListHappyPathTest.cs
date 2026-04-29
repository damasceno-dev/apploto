using System.Globalization;
using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerListHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_ShouldReturnOnlyAuthenticatedBranchRows()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("DcListBranchA");
        var (_, branchB, _, tokenB) = await factory.SeedFullBranchContextAsync("DcListBranchB");
        var accountA = await factory.SeedAccountAsync(branchA.Id, AccountType.Terminal);
        var accountB = await factory.SeedAccountAsync(branchB.Id, AccountType.Terminal);
        var closeA = await factory.SeedDailyCloseAsync(branchA.Id, accountA.Id);
        var closeB = await factory.SeedDailyCloseAsync(branchB.Id, accountB.Id);

        var payload = await GetListAsync("/dailyclose", tokenA);

        payload.Items.Select(item => item.Id).ShouldContain(closeA.Id);
        payload.Items.Select(item => item.Id).ShouldNotContain(closeB.Id);
        payload.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_ShouldApplySupportedFilters()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcListFilters");
        var accountA = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var accountB = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var operatorA = await factory.SeedOperatorAsync(branch.Id);
        var operatorB = await factory.SeedOperatorAsync(branch.Id);

        var accountMatch = await factory.SeedDailyCloseAsync(
            branch.Id, accountA.Id, date: new DateTime(2025, 2, 10), submittedByOperatorId: operatorA.Id);
        var accountMiss = await factory.SeedDailyCloseAsync(
            branch.Id, accountB.Id, date: new DateTime(2025, 2, 10), submittedByOperatorId: operatorA.Id);
        var submittedClose = await factory.SeedDailyCloseAsync(
            branch.Id, accountA.Id, date: new DateTime(2025, 2, 11),
            status: DailyCloseStatus.Submitted, submittedByOperatorId: operatorA.Id);
        var outsideDate = await factory.SeedDailyCloseAsync(
            branch.Id, accountA.Id, date: new DateTime(2025, 1, 1), submittedByOperatorId: operatorA.Id);
        var operatorMatch = await factory.SeedDailyCloseAsync(
            branch.Id, accountA.Id, date: new DateTime(2025, 2, 12), submittedByOperatorId: operatorB.Id);

        var byAccount = await GetListAsync($"/dailyclose?accountId={accountA.Id}", token);
        byAccount.Items.Select(item => item.Id).ShouldContain(accountMatch.Id);
        byAccount.Items.Select(item => item.Id).ShouldNotContain(accountMiss.Id);
        byAccount.Items.All(item => item.AccountId == accountA.Id).ShouldBeTrue();

        var byStatus = await GetListAsync($"/dailyclose?status={(int)DailyCloseStatus.Submitted}", token);
        byStatus.Items.Select(item => item.Id).ShouldContain(submittedClose.Id);
        byStatus.Items.All(item => item.Status == DailyCloseStatus.Submitted).ShouldBeTrue();

        var byDateRange = await GetListAsync(
            $"/dailyclose?dateFrom={QueryDate(new DateTime(2025, 2, 10))}&dateTo={QueryDate(new DateTime(2025, 2, 11))}",
            token);
        byDateRange.Items.Select(item => item.Id).ShouldContain(accountMatch.Id);
        byDateRange.Items.Select(item => item.Id).ShouldContain(submittedClose.Id);
        byDateRange.Items.Select(item => item.Id).ShouldNotContain(outsideDate.Id);

        var byOperator = await GetListAsync($"/dailyclose?operatorId={operatorB.Id}", token);
        byOperator.Items.Select(item => item.Id).ShouldBe([operatorMatch.Id]);
    }

    [Fact]
    public async Task List_ShouldResolveMineToAuthenticatedLinkedOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcListMine");
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var otherOperator = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var ownClose = await factory.SeedDailyCloseAsync(
            branch.Id, account.Id, submittedByOperatorId: callerOperator.Id);
        var otherClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: DateTime.Today.AddDays(-1),
            submittedByOperatorId: otherOperator.Id);

        var payload = await GetListAsync("/dailyclose?mine=true", token);

        payload.Items.Select(item => item.Id).ShouldBe([ownClose.Id]);
        payload.Items.Select(item => item.Id).ShouldNotContain(otherClose.Id);
        payload.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_ShouldNarrowMemberRowsToLinkedAccounts()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcListMember", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var otherOperator = await factory.SeedOperatorAsync(branch.Id);
        var accountA = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var accountB = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, accountA.Id);
        await factory.SeedOperatorAccountAsync(otherOperator.Id, accountB.Id);

        var visible = await factory.SeedDailyCloseAsync(branch.Id, accountA.Id,
            submittedByOperatorId: callerOperator.Id);
        var hidden = await factory.SeedDailyCloseAsync(branch.Id, accountB.Id,
            submittedByOperatorId: otherOperator.Id);

        var payload = await GetListAsync("/dailyclose", token);

        payload.Items.Select(item => item.Id).ShouldBe([visible.Id]);
        payload.Items.Select(item => item.Id).ShouldNotContain(hidden.Id);
        payload.TotalCount.ShouldBe(1);

        var explicitUnlinked = await GetListAsync($"/dailyclose?accountId={accountB.Id}", token);
        explicitUnlinked.Items.ShouldBeEmpty();
        explicitUnlinked.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_ShouldOrderByDateCreatedAtAndIdDescending()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcListOrdering");
        var accountDateWins = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var accountCreatedAtWins = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var accountIdWinsHigh = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var accountIdWinsLow = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var sameCreatedLowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sameCreatedHighId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var sameDate = new DateTime(2025, 3, 1);
        var sharedCreatedAt = new DateTime(2025, 3, 1, 9, 0, 0, DateTimeKind.Utc);

        var dateWins = await factory.SeedDailyCloseAsync(
            branch.Id,
            accountDateWins.Id,
            date: new DateTime(2025, 3, 2),
            createdAt: new DateTime(2025, 3, 1, 8, 0, 0, DateTimeKind.Utc));
        var createdAtWins = await factory.SeedDailyCloseAsync(
            branch.Id,
            accountCreatedAtWins.Id,
            date: sameDate,
            createdAt: new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        var idWinsHigh = await factory.SeedDailyCloseAsync(
            branch.Id,
            accountIdWinsHigh.Id,
            date: sameDate,
            createdAt: sharedCreatedAt,
            id: sameCreatedHighId);
        var idWinsLow = await factory.SeedDailyCloseAsync(
            branch.Id,
            accountIdWinsLow.Id,
            date: sameDate,
            createdAt: sharedCreatedAt,
            id: sameCreatedLowId);

        var payload = await GetListAsync("/dailyclose", token);

        payload.Items.Select(item => item.Id).Take(4).ShouldBe([
            dateWins.Id,
            createdAtWins.Id,
            idWinsHigh.Id,
            idWinsLow.Id
        ]);
    }

    [Theory]
    [InlineData(1, 3, true, false)]
    [InlineData(2, 3, true, true)]
    [InlineData(3, 1, false, true)]
    public async Task List_ShouldPageSevenRowsWithPageSizeThreeAndExposePagingMetadata(
        int page,
        int expectedItemCount,
        bool expectedHasNext,
        bool expectedHasPrevious)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync($"DcListPaginationP{page}");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        foreach (var index in Enumerable.Range(1, 7))
        {
            await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: new DateTime(2025, 4, index));
        }

        var payload = await GetListAsync($"/dailyclose?accountId={account.Id}&page={page}&pageSize=3", token);

        payload.Items.Count.ShouldBe(expectedItemCount);
        payload.TotalCount.ShouldBe(7);
        payload.TotalPages.ShouldBe(3);
        payload.HasNext.ShouldBe(expectedHasNext);
        payload.HasPrevious.ShouldBe(expectedHasPrevious);
    }

    [Fact]
    public async Task List_ShouldExposeJoinedNamesOnItems()
    {
        var (approvedByUser, branch, _, token) = await factory.SeedFullBranchContextAsync("DcListNames");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, name: "Caixa Exibição");
        var op = await factory.SeedOperatorAsync(branch.Id, name: "Operador Exibição");
        await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            submittedByOperatorId: op.Id,
            approvedByUserId: approvedByUser.Id,
            approvedAt: DateTime.UtcNow);

        var payload = await GetListAsync($"/dailyclose?accountId={account.Id}", token);

        payload.Items.Count.ShouldBe(1);
        var item = payload.Items[0];
        item.AccountName.ShouldBe("Caixa Exibição");
        item.SubmittedByOperatorName.ShouldBe("Operador Exibição");
        item.ApprovedByUserName.ShouldBe(approvedByUser.Name);
    }

    [Fact]
    public async Task List_ShouldReturn200WithEmptyItems_WhenMemberHasLinkedOperatorButNoActiveAccounts()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcListEmptyScope", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        // No SeedOperatorAccountAsync calls — operator has zero active accounts.
        var otherOperator = await factory.SeedOperatorAsync(branch.Id);
        var otherAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedDailyCloseAsync(branch.Id, otherAccount.Id,
            submittedByOperatorId: otherOperator.Id);

        var payload = await GetListAsync("/dailyclose", token);

        payload.Items.ShouldBeEmpty();
        payload.TotalCount.ShouldBe(0);
        payload.TotalPages.ShouldBe(0);
        payload.HasNext.ShouldBeFalse();
        payload.HasPrevious.ShouldBeFalse();
    }

    private async Task<ResponseListDailyClosesJson> GetListAsync(string uri, string token)
    {
        var response = await _client.GetAuthAsync(uri, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseListDailyClosesJson>();
    }

    private static string QueryDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
