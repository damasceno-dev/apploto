using System.Globalization;
using System.Net;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 7.6.3 (gap Z): <c>GET /transaction?transactionTypeId=</c> narrows the list with the other filters
/// before paging and counts, inside the unchanged branch and Member account scope.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionControllerListTypeFilterTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_ShouldCombineTypeWithDraftAndDateFilters_WithExactTotalCountAndPageBoundaries()
    {
        var ctx = await SeedContextAsync("TxnListTypeDraftDate", Role.Manager);
        var otherType = await factory.SeedTransactionTypeAsync(ctx.CategoryId, name: "Outro tipo");
        var expected = new List<Guid>();
        foreach (var day in Enumerable.Range(1, 5))
        {
            expected.Add((await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 5, day), TransactionStatus.Draft)).Id);
        }

        var outsideDate = await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 6, 10), TransactionStatus.Draft);
        var activeSameType = await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 5, 6), TransactionStatus.Active);
        var draftOtherType = await SeedAsync(ctx, ctx.AccountId, otherType.Id, new DateTime(2025, 5, 7), TransactionStatus.Draft);
        var query =
            $"/transaction?transactionTypeId={ctx.TypeId}&status={(int)TransactionStatus.Draft}" +
            $"&dateFrom={QueryDate(new DateTime(2025, 5, 1))}&dateTo={QueryDate(new DateTime(2025, 5, 31))}&pageSize=2";

        var page1 = await GetListAsync($"{query}&page=1", ctx.Token);
        var page2 = await GetListAsync($"{query}&page=2", ctx.Token);
        var page3 = await GetListAsync($"{query}&page=3", ctx.Token);
        var page4 = await GetListAsync($"{query}&page=4", ctx.Token);

        page1.Items.Count.ShouldBe(2);
        page1.HasPrevious.ShouldBeFalse();
        page1.HasNext.ShouldBeTrue();
        page2.Items.Count.ShouldBe(2);
        page2.HasPrevious.ShouldBeTrue();
        page2.HasNext.ShouldBeTrue();
        page3.Items.Count.ShouldBe(1);
        page3.HasPrevious.ShouldBeTrue();
        page3.HasNext.ShouldBeFalse();
        page4.Items.ShouldBeEmpty();
        foreach (var page in new[] { page1, page2, page3, page4 })
        {
            page.TotalCount.ShouldBe(5);
            page.TotalPages.ShouldBe(3);
        }

        var returned = page1.Items.Concat(page2.Items).Concat(page3.Items).ToList();
        returned.Select(item => item.Id).ShouldBe(expected.AsEnumerable().Reverse());
        returned.ShouldAllBe(item => item.TransactionTypeId == ctx.TypeId && item.Status == TransactionStatus.Draft);
        returned.Select(item => item.Id).ShouldNotContain(outsideDate.Id);
        returned.Select(item => item.Id).ShouldNotContain(activeSameType.Id);
        returned.Select(item => item.Id).ShouldNotContain(draftOtherType.Id);

        var typeOnly = await GetListAsync($"/transaction?transactionTypeId={ctx.TypeId}", ctx.Token);
        typeOnly.TotalCount.ShouldBe(7);
        typeOnly.Items.ShouldAllBe(item => item.TransactionTypeId == ctx.TypeId);
    }

    [Fact]
    public async Task List_ShouldReturnAnEmptyPage_WhenNoTransactionHasTheType()
    {
        var ctx = await SeedContextAsync("TxnListTypeNoMatch", Role.Manager);
        await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 5, 1), TransactionStatus.Active);
        var unusedType = await factory.SeedTransactionTypeAsync(ctx.CategoryId, name: "Sem lançamentos");

        foreach (var typeId in new[] { unusedType.Id, Guid.NewGuid() })
        {
            var payload = await GetListAsync($"/transaction?transactionTypeId={typeId}", ctx.Token);

            payload.Items.ShouldBeEmpty();
            payload.TotalCount.ShouldBe(0);
            payload.HasNext.ShouldBeFalse();
            payload.HasPrevious.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task List_ShouldStillFilterByARetiredTypeThatCannotBeUsedForNewTransactions()
    {
        var ctx = await SeedContextAsync("TxnListTypeRetired", Role.Manager);
        var retiredType = await factory.SeedTransactionTypeAsync(ctx.CategoryId, name: "Tipo desativado", active: false);
        var historical = await SeedAsync(ctx, ctx.AccountId, retiredType.Id, new DateTime(2025, 5, 1), TransactionStatus.Active);
        await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 5, 2), TransactionStatus.Active);

        var payload = await GetListAsync($"/transaction?transactionTypeId={retiredType.Id}", ctx.Token);

        payload.Items.Select(item => item.Id).ShouldBe([historical.Id]);
        payload.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_ShouldNotCrossBranches_WhenFilteringByAnotherBranchType()
    {
        var ctx = await SeedContextAsync("TxnListTypeBranchA", Role.Manager);
        var other = await SeedContextAsync("TxnListTypeBranchB", Role.Manager);
        await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 5, 1), TransactionStatus.Active);
        var otherRow = await SeedAsync(other, other.AccountId, other.TypeId, new DateTime(2025, 5, 1), TransactionStatus.Active);

        var foreignType = await GetListAsync($"/transaction?transactionTypeId={other.TypeId}", ctx.Token);
        var ownTypeFromOtherBranch = await GetListAsync($"/transaction?transactionTypeId={ctx.TypeId}", other.Token);
        var ownTypeInOwnBranch = await GetListAsync($"/transaction?transactionTypeId={other.TypeId}", other.Token);

        foreignType.Items.ShouldBeEmpty();
        foreignType.TotalCount.ShouldBe(0);
        ownTypeFromOtherBranch.Items.ShouldBeEmpty();
        ownTypeFromOtherBranch.TotalCount.ShouldBe(0);
        ownTypeInOwnBranch.Items.Select(item => item.Id).ShouldBe([otherRow.Id]);
    }

    [Fact]
    public async Task List_ShouldKeepMemberAccountScope_WhenFilteringByType()
    {
        var ctx = await SeedContextAsync("TxnListTypeMember", Role.Member);
        var unlinkedAccount = await factory.SeedAccountAsync(ctx.BranchId, AccountType.Terminal);
        var otherType = await factory.SeedTransactionTypeAsync(ctx.CategoryId, name: "Outro tipo");
        var visibleOlder = await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 5, 1), TransactionStatus.Active);
        var visibleNewer = await SeedAsync(ctx, ctx.AccountId, ctx.TypeId, new DateTime(2025, 5, 2), TransactionStatus.Draft);
        await SeedAsync(ctx, ctx.AccountId, otherType.Id, new DateTime(2025, 5, 3), TransactionStatus.Active);
        await SeedAsync(ctx, unlinkedAccount.Id, ctx.TypeId, new DateTime(2025, 5, 4), TransactionStatus.Active);
        await SeedAsync(ctx, unlinkedAccount.Id, ctx.TypeId, new DateTime(2025, 5, 5), TransactionStatus.Active);

        var scoped = await GetListAsync($"/transaction?transactionTypeId={ctx.TypeId}&pageSize=1", ctx.Token);
        var scopedPage2 = await GetListAsync($"/transaction?transactionTypeId={ctx.TypeId}&pageSize=1&page=2", ctx.Token);
        var explicitUnlinked = await GetListAsync(
            $"/transaction?transactionTypeId={ctx.TypeId}&accountId={unlinkedAccount.Id}",
            ctx.Token);

        scoped.Items.Select(item => item.Id).ShouldBe([visibleNewer.Id]);
        scoped.TotalCount.ShouldBe(2);
        scoped.TotalPages.ShouldBe(2);
        scoped.HasNext.ShouldBeTrue();
        scopedPage2.Items.Select(item => item.Id).ShouldBe([visibleOlder.Id]);
        scopedPage2.HasNext.ShouldBeFalse();
        explicitUnlinked.Items.ShouldBeEmpty();
        explicitUnlinked.TotalCount.ShouldBe(0);
    }

    private async Task<ResponseListTransactionsJson> GetListAsync(string uri, string token)
    {
        var response = await _client.GetAuthAsync(uri, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseListTransactionsJson>();
    }

    private async Task<TypeFilterContext> SeedContextAsync(string label, Role role)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, role);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id, isPrimary: true);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, name: "Tipo filtrado");
        return new TypeFilterContext(branch.Id, user.Id, op.Id, account.Id, category.Id, transactionType.Id, token);
    }

    private Task<Transaction> SeedAsync(
        TypeFilterContext ctx,
        Guid accountId,
        Guid transactionTypeId,
        DateTime date,
        TransactionStatus status)
    {
        return factory.SeedTransactionAsync(
            ctx.BranchId,
            accountId,
            transactionTypeId,
            ctx.CategoryId,
            Direction.In,
            ctx.OperatorId,
            ctx.UserId,
            date: date,
            status: status);
    }

    private static string QueryDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed record TypeFilterContext(
        Guid BranchId,
        Guid UserId,
        Guid OperatorId,
        Guid AccountId,
        Guid CategoryId,
        Guid TypeId,
        string Token);
}
