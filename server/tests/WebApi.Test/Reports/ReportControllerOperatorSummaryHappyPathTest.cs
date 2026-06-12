using System.Net;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerOperatorSummaryHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // Manager queries any branch operator over a multi-category matrix.
    // Draft, Cancelled, out-of-range, and other-operator rows are excluded.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldAggregateByCategory_WhenManagerQueriesOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OSHappy1", Role.Manager);
        var targetOperator = await factory.SeedOperatorAsync(branch.Id, "Operadora Alvo");
        var otherOperator = await factory.SeedOperatorAsync(branch.Id, "Operadora Ruído");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var categoryA = await factory.SeedCategoryAsync(branch.Id, "Apostas");
        var categoryB = await factory.SeedCategoryAsync(branch.Id, "Despesas", Direction.Out);
        var typeA = await factory.SeedTransactionTypeAsync(categoryA.Id);
        var typeB = await factory.SeedTransactionTypeAsync(categoryB.Id);

        var day = new DateTime(2025, 3, 10);

        var inA1 = await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.In,
            targetOperator.Id, user.Id, date: day, value: 100m);
        var inA2 = await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.In,
            targetOperator.Id, user.Id, date: day.AddDays(1), value: 50m);
        var outA = await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.Out,
            targetOperator.Id, user.Id, date: day, value: 30m);
        var outB = await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeB.Id, categoryB.Id, Direction.Out,
            targetOperator.Id, user.Id, date: day, value: 80m);

        // Excluded rows: Draft, Cancelled, outside the window, other operator.
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.In,
            targetOperator.Id, user.Id, date: day, value: 999m, status: TransactionStatus.Draft);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.In,
            targetOperator.Id, user.Id, date: day, value: 999m, status: TransactionStatus.Cancelled);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.In,
            targetOperator.Id, user.Id, date: new DateTime(2025, 5, 1), value: 999m);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.In,
            otherOperator.Id, user.Id, date: day, value: 999m);

        // Reload-based expectations: totals must match persisted values, not seed input.
        var reloadedIn1 = await factory.ReloadAsync<Transaction>(inA1.Id);
        var reloadedIn2 = await factory.ReloadAsync<Transaction>(inA2.Id);
        var reloadedOutA = await factory.ReloadAsync<Transaction>(outA.Id);
        var reloadedOutB = await factory.ReloadAsync<Transaction>(outB.Id);
        reloadedIn1.ShouldNotBeNull();
        reloadedIn2.ShouldNotBeNull();
        reloadedOutA.ShouldNotBeNull();
        reloadedOutB.ShouldNotBeNull();
        var expectedTotalIn = reloadedIn1.Value + reloadedIn2.Value;
        var expectedTotalOut = reloadedOutA.Value + reloadedOutB.Value;

        var url = $"/report/operator-summary?operatorId={targetOperator.Id}&dateFrom=2025-03-01&dateTo=2025-03-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.OperatorId.ShouldBe(targetOperator.Id);
        payload.OperatorName.ShouldBe("Operadora Alvo");
        payload.TotalTransactionCount.ShouldBe(4);
        payload.TotalInValue.ShouldBe(expectedTotalIn);
        payload.TotalOutValue.ShouldBe(expectedTotalOut);
        payload.NetValue.ShouldBe(expectedTotalIn - expectedTotalOut);

        payload.ByCategory.Count.ShouldBe(2);
        payload.ByCategory[0].CategoryId.ShouldBe(categoryA.Id);
        payload.ByCategory[0].CategoryName.ShouldBe("Apostas");
        payload.ByCategory[0].Count.ShouldBe(3);
        payload.ByCategory[0].TotalIn.ShouldBe(reloadedIn1.Value + reloadedIn2.Value);
        payload.ByCategory[0].TotalOut.ShouldBe(reloadedOutA.Value);
        payload.ByCategory[1].CategoryId.ShouldBe(categoryB.Id);
        payload.ByCategory[1].CategoryName.ShouldBe("Despesas");
        payload.ByCategory[1].Count.ShouldBe(1);
        payload.ByCategory[1].TotalIn.ShouldBe(0m);
        payload.ByCategory[1].TotalOut.ShouldBe(reloadedOutB.Value);
    }

    // -------------------------------------------------------------------------
    // ByCategory ordering: CategoryName ASC even when seeded out of order
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldOrderCategoriesByName_WhenSeededOutOfOrder()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OSHappy2", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var categoryZ = await factory.SeedCategoryAsync(branch.Id, "Zebra");
        var categoryA = await factory.SeedCategoryAsync(branch.Id, "Abelha");
        var typeZ = await factory.SeedTransactionTypeAsync(categoryZ.Id);
        var typeA = await factory.SeedTransactionTypeAsync(categoryA.Id);

        var day = new DateTime(2025, 4, 5);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeZ.Id, categoryZ.Id, Direction.In, op.Id, user.Id, date: day, value: 10m);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, typeA.Id, categoryA.Id, Direction.In, op.Id, user.Id, date: day, value: 20m);

        var url = $"/report/operator-summary?operatorId={op.Id}&dateFrom=2025-04-01&dateTo=2025-04-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.ByCategory.Count.ShouldBe(2);
        payload.ByCategory[0].CategoryName.ShouldBe("Abelha");
        payload.ByCategory[1].CategoryName.ShouldBe("Zebra");
    }

    // -------------------------------------------------------------------------
    // Member self via Mine = true — only rows on linked accounts are counted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturnOwnScopedSummary_WhenMemberUsesMine()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OSHappy3", Role.Member);
        var linkedOperator = await factory.SeedOperatorAsync(branch.Id, "Operadora Membro", userId: user.Id);
        var linkedAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Conta Vinculada");
        var unlinkedAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Conta Avulsa");
        await factory.SeedOperatorAccountAsync(linkedOperator.Id, linkedAccount.Id, isPrimary: true);

        var category = await factory.SeedCategoryAsync(branch.Id, "Apostas");
        var type = await factory.SeedTransactionTypeAsync(category.Id);

        var day = new DateTime(2025, 6, 10);
        var inScope = await factory.SeedTransactionAsync(
            branch.Id, linkedAccount.Id, type.Id, category.Id, Direction.In,
            linkedOperator.Id, user.Id, date: day, value: 70m);

        // Recorded by the member's own operator but on an account outside the
        // linked set — excluded by AllowedAccountIds.
        await factory.SeedTransactionAsync(
            branch.Id, unlinkedAccount.Id, type.Id, category.Id, Direction.In,
            linkedOperator.Id, user.Id, date: day, value: 999m);

        var reloaded = await factory.ReloadAsync<Transaction>(inScope.Id);
        reloaded.ShouldNotBeNull();

        const string url = "/report/operator-summary?mine=true&dateFrom=2025-06-01&dateTo=2025-06-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.OperatorId.ShouldBe(linkedOperator.Id);
        payload.OperatorName.ShouldBe("Operadora Membro");
        payload.TotalTransactionCount.ShouldBe(1);
        payload.TotalInValue.ShouldBe(reloaded.Value);
        payload.TotalOutValue.ShouldBe(0m);
        payload.NetValue.ShouldBe(reloaded.Value);
        payload.ByCategory.Count.ShouldBe(1);
        payload.ByCategory[0].Count.ShouldBe(1);
        payload.ByCategory[0].TotalIn.ShouldBe(reloaded.Value);
    }

    // -------------------------------------------------------------------------
    // Member with explicit own OperatorId resolves like Mine = true
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn200_WhenMemberSuppliesOwnOperatorId()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OSHappy4", Role.Member);
        var linkedOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(linkedOperator.Id, account.Id, isPrimary: true);

        var category = await factory.SeedCategoryAsync(branch.Id, "Apostas");
        var type = await factory.SeedTransactionTypeAsync(category.Id);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, type.Id, category.Id, Direction.In,
            linkedOperator.Id, user.Id, date: new DateTime(2025, 7, 2), value: 45m);

        var url = $"/report/operator-summary?operatorId={linkedOperator.Id}&dateFrom=2025-07-01&dateTo=2025-07-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.OperatorId.ShouldBe(linkedOperator.Id);
        payload.TotalTransactionCount.ShouldBe(1);
        payload.TotalInValue.ShouldBe(45m);
    }

    // -------------------------------------------------------------------------
    // Member omitting both mine and operatorId falls back to own operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturnOwnSummary_WhenMemberOmitsMineAndOperatorId()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OSHappy7", Role.Member);
        var linkedOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(linkedOperator.Id, account.Id, isPrimary: true);

        var category = await factory.SeedCategoryAsync(branch.Id, "Apostas");
        var type = await factory.SeedTransactionTypeAsync(category.Id);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, type.Id, category.Id, Direction.In,
            linkedOperator.Id, user.Id, date: new DateTime(2025, 9, 3), value: 55m);

        const string url = "/report/operator-summary?dateFrom=2025-09-01&dateTo=2025-09-30";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.OperatorId.ShouldBe(linkedOperator.Id);
        payload.TotalTransactionCount.ShouldBe(1);
        payload.TotalInValue.ShouldBe(55m);
    }

    // -------------------------------------------------------------------------
    // Member with no linked operator — empty summary short-circuit, not 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturnEmptySummary_WhenMemberHasNoLinkedOperator()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OSHappy5", Role.Member);

        const string url = "/report/operator-summary?mine=true&dateFrom=2025-01-01&dateTo=2025-01-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.OperatorId.ShouldBe(Guid.Empty);
        payload.OperatorName.ShouldBe(string.Empty);
        payload.TotalTransactionCount.ShouldBe(0);
        payload.TotalInValue.ShouldBe(0m);
        payload.TotalOutValue.ShouldBe(0m);
        payload.NetValue.ShouldBe(0m);
        payload.ByCategory.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Member with no linked operator gets the empty summary even when naming
    // another operator explicitly — the short-circuit wins over the 403 path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturnEmptySummary_WhenUnlinkedMemberSuppliesExplicitOperatorId()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OSHappy8", Role.Member);
        var otherOperator = await factory.SeedOperatorAsync(branch.Id);

        var url = $"/report/operator-summary?operatorId={otherOperator.Id}&dateFrom=2025-01-01&dateTo=2025-01-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.OperatorId.ShouldBe(Guid.Empty);
        payload.OperatorName.ShouldBe(string.Empty);
        payload.TotalTransactionCount.ShouldBe(0);
        payload.ByCategory.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Branch isolation: another branch's rows never leak into the summary
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldExcludeOtherBranchRows_WhenAnotherBranchHasActivity()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OSHappy6", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Apostas");
        var type = await factory.SeedTransactionTypeAsync(category.Id);

        var otherBranch = await factory.SeedBranchForOtherContextAsync("OSHappy6-other");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id, "Apostas");
        var otherType = await factory.SeedTransactionTypeAsync(otherCategory.Id);

        var day = new DateTime(2025, 8, 5);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, type.Id, category.Id, Direction.In, op.Id, user.Id, date: day, value: 25m);
        await factory.SeedTransactionAsync(
            otherBranch.Id, otherAccount.Id, otherType.Id, otherCategory.Id, Direction.In,
            op.Id, user.Id, date: day, value: 999m);

        var url = $"/report/operator-summary?operatorId={op.Id}&dateFrom=2025-08-01&dateTo=2025-08-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();

        payload.TotalTransactionCount.ShouldBe(1);
        payload.TotalInValue.ShouldBe(25m);
    }
}
