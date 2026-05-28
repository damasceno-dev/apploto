using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerOpenChequeAgingHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // End-to-end: 6-row plan, pay rows 1-2, assert group shape
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldReturnOneGroupWith4OpenRows_WhenTwoOfSixInstallmentsPaid()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy1", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var client = await factory.SeedClientAsync(branch.Id, "ClientPlan");

        // baseDue must be > today for FirstDueDateIsInFuture validation to pass
        var baseDue = DateTime.Today.AddDays(30);
        // asOfDate is 50 days after row 3's due date so row3 falls in Days31To60
        var asOfDate = baseDue.AddMonths(2).AddDays(50);

        // Seed 6 installments with known due dates and values
        var installmentItems = Enumerable.Range(0, 6).Select(i =>
            new RequestCreateTransactionInstallmentItemJson
            {
                DueDate = baseDue.AddMonths(i),
                Value = 100m
            }).ToArray();

        var installRequest = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(new DateTime(2025, 1, 15))
            .WithDescription("Cheque plano")
            .WithValue(600m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithClientId(client.Id)
            .WithInstallments(installmentItems)
            .Build();

        var createResp = await _client.PostAuthAsync("/transaction/installment", installRequest, token);
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResp.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();

        var row1Id = created.Installments[0].Id;
        var row2Id = created.Installments[1].Id;
        var originId = row1Id;

        // Mark rows 1 and 2 as paid directly via DB (we are testing the report, not the update endpoint)
        await MarkTransactionAsPaidAsync(row1Id, DateTime.UtcNow.AddDays(-5));
        await MarkTransactionAsPaidAsync(row2Id, DateTime.UtcNow.AddDays(-4));

        var url = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOpenChequeAgingJson>();
        payload.AsOfDate.Date.ShouldBe(asOfDate.Date);
        payload.TotalCount.ShouldBe(1);
        payload.Items.Count.ShouldBe(1);

        var group = payload.Items[0];
        group.OriginTransactionId.ShouldBe(originId);
        group.TotalRowCount.ShouldBe(6);
        group.OpenRowCount.ShouldBe(4);
        group.OutstandingTotal.ShouldBe(400m);
        group.OldestOpenDueDate.Date.ShouldBe(baseDue.AddMonths(2).Date);
        group.Rows.Count.ShouldBe(4);

        // Row 3 due: baseDue + 2 months (50 days before asOfDate) => Days31To60
        var row3Due = baseDue.AddMonths(2);
        var row3 = group.Rows.FirstOrDefault(r => r.DueDate.Date == row3Due.Date);
        row3.ShouldNotBeNull();
        row3.Bucket.ShouldBe(AgingBucket.Days31To60);
        row3.Value.ShouldBe(100m);
    }

    // -------------------------------------------------------------------------
    // Per-bucket boundaries for the OldestOpenBucket
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldAssignCorrectOldestOpenBucket_WhenDueDateIsExactBoundary()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy2", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var client = await factory.SeedClientAsync(branch.Id, "ClientBuckets");

        var asOfDate = new DateTime(2025, 6, 30);

        // Group A: oldest due = asOfDate - 30 days => Days0To30
        var dueDateA = asOfDate.AddDays(-30);
        var originA = Guid.NewGuid();
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 1, 1), value: 100m, clientId: client.Id,
            dueDate: dueDateA, originTransactionId: originA, id: originA);

        // Group B: oldest due = asOfDate - 91 days => Days91Plus
        var dueDateB = asOfDate.AddDays(-91);
        var originB = Guid.NewGuid();
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2024, 12, 1), value: 200m, clientId: client.Id,
            dueDate: dueDateB, originTransactionId: originB, id: originB);

        var url = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOpenChequeAgingJson>();
        payload.TotalCount.ShouldBe(2);

        var groupA = payload.Items.FirstOrDefault(g => g.OriginTransactionId == originA);
        groupA.ShouldNotBeNull();
        groupA.OldestOpenBucket.ShouldBe(AgingBucket.Days0To30);

        var groupB = payload.Items.FirstOrDefault(g => g.OriginTransactionId == originB);
        groupB.ShouldNotBeNull();
        groupB.OldestOpenBucket.ShouldBe(AgingBucket.Days91Plus);
    }

    // -------------------------------------------------------------------------
    // Paid rows excluded from groups entirely
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldExcludeFullyPaidGroup_WhenAllSiblingsArePaid()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy3", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var client = await factory.SeedClientAsync(branch.Id, "ClientPaid");

        var asOfDate = new DateTime(2025, 6, 30);
        var originPaid = Guid.NewGuid();

        // Both rows paid
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 1, 1), value: 100m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-10), originTransactionId: originPaid, id: originPaid,
            paidAt: asOfDate.AddDays(-5));
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 1, 2), value: 100m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-5), originTransactionId: originPaid,
            paidAt: asOfDate.AddDays(-3));

        var url = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOpenChequeAgingJson>();
        payload.TotalCount.ShouldBe(0);
        payload.Items.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Non-installment rows (OriginTransactionId = null) never grouped
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldNotIncludeNonInstallmentRows_WhenOriginIsNull()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy4", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var client = await factory.SeedClientAsync(branch.Id);

        var asOfDate = new DateTime(2025, 6, 30);

        // Ordinary transaction with no OriginTransactionId
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 500m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-5));

        var url = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOpenChequeAgingJson>();
        payload.TotalCount.ShouldBe(0);
        payload.Items.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Per-branch isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldIsolatePerBranch_WhenAnotherBranchHasActivity()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy5", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("OCAHappy5-other");

        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var client = await factory.SeedClientAsync(branch.Id);

        var asOfDate = new DateTime(2025, 6, 30);
        var originId = Guid.NewGuid();
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 1, 1), value: 100m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-10), originTransactionId: originId, id: originId);

        // Other-branch installment row — must not appear
        var otherOp = await factory.SeedOperatorAsync(otherBranch.Id);
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id);
        var otherType = await factory.SeedTransactionTypeAsync(
            otherCategory.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var otherClient = await factory.SeedClientAsync(otherBranch.Id);
        var otherOriginId = Guid.NewGuid();
        await factory.SeedTransactionAsync(
            otherBranch.Id, otherAccount.Id, otherType.Id, otherCategory.Id,
            Direction.Out, otherOp.Id, user.Id,
            date: new DateTime(2025, 1, 1), value: 999m, clientId: otherClient.Id,
            dueDate: asOfDate.AddDays(-10), originTransactionId: otherOriginId, id: otherOriginId);

        var url = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOpenChequeAgingJson>();
        payload.TotalCount.ShouldBe(1);
        payload.Items[0].OriginTransactionId.ShouldBe(originId);
    }

    // -------------------------------------------------------------------------
    // Cancelled installment rows are excluded
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldExcludeCancelledInstallmentRows_WhenStatusIsCancelled()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy8", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var client = await factory.SeedClientAsync(branch.Id);

        var asOfDate = new DateTime(2025, 6, 30);
        var originId = Guid.NewGuid();

        // Both rows have OriginTransactionId set but are Cancelled — must not appear in aging
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 1, 1), value: 100m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-10), originTransactionId: originId, id: originId,
            status: TransactionStatus.Cancelled);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 1, 2), value: 100m, clientId: client.Id,
            dueDate: asOfDate.AddDays(-5), originTransactionId: originId,
            status: TransactionStatus.Cancelled);

        var url = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOpenChequeAgingJson>();
        payload.TotalCount.ShouldBe(0);
        payload.Items.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Admin can access
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldReturn200_WhenAdminRequests()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy6", Role.Admin);
        var url = "/report/cheques/open-aging?page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // Pagination
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OpenChequeAging_ShouldReturnCorrectPagination_WhenMultipleGroups()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAHappy7", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        var client = await factory.SeedClientAsync(branch.Id);

        var asOfDate = new DateTime(2025, 6, 30);

        // Seed 5 single-row installment groups
        for (var i = 1; i <= 5; i++)
        {
            var oid = Guid.NewGuid();
            await factory.SeedTransactionAsync(
                branch.Id, account.Id, transactionType.Id, category.Id,
                Direction.Out, op.Id, user.Id,
                date: new DateTime(2025, 1, i), value: i * 50m, clientId: client.Id,
                dueDate: asOfDate.AddDays(-i), originTransactionId: oid, id: oid);
        }

        var url1 = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=3";
        var r1 = await _client.GetAuthAsync(url1, token);
        var p1 = await r1.ReadContentAsync<ResponseOpenChequeAgingJson>();
        p1.TotalCount.ShouldBe(5);
        p1.TotalPages.ShouldBe(2);
        p1.Items.Count.ShouldBe(3);
        p1.HasPrevious.ShouldBeFalse();
        p1.HasNext.ShouldBeTrue();

        var url2 = $"/report/cheques/open-aging?asOfDate={asOfDate:yyyy-MM-dd}&page=2&pageSize=3";
        var r2 = await _client.GetAuthAsync(url2, token);
        var p2 = await r2.ReadContentAsync<ResponseOpenChequeAgingJson>();
        p2.Items.Count.ShouldBe(2);
        p2.HasPrevious.ShouldBeTrue();
        p2.HasNext.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<server.Domain.Entities.Transaction?> LoadTransactionAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
    }

    private async Task MarkTransactionAsPaidAsync(Guid id, DateTime paidAt)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var tx = await dbContext.Transactions.FindAsync(id);
        if (tx is null) return;
        tx.PaidAt = paidAt;
        await dbContext.SaveChangesAsync();
    }
}
