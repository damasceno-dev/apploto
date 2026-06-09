using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerEditPreviewHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EditPreview_ShouldReturn200WithImpactShape_AndLeaveTheRowByteForByteUnchanged()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewHappy", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var originalClient = await factory.SeedClientAsync(branch.Id, "Original Client");
        var newClient = await factory.SeedClientAsync(branch.Id, "New Client");

        var asOfDate = new DateTime(2025, 6, 30);
        var transactionDate = new DateTime(2025, 3, 1);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: tabAccount.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: transactionDate,
            value: 100m,
            description: "original description",
            transactionTime: new TimeOnly(9, 0),
            clientId: originalClient.Id,
            dueDate: asOfDate.AddDays(-5),
            paidAt: null);

        // Shift the due date from Days0To30 (5 days late) to Days31To60 (60 days late), keep
        // the row unpaid, and reassign the client.
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("hypothetical description")
            .WithDueDate(asOfDate.AddDays(-60))
            .WithPaidAt(null)
            .WithClientId(newClient.Id)
            .WithTransactionTime(new TimeOnly(14, 0))
            .Build();

        var before = await factory.ReloadAsync<Transaction>(transaction.Id);
        before.ShouldNotBeNull();

        var url = $"/transaction/{transaction.Id}/edit-preview?asOfDate={asOfDate:yyyy-MM-dd}";
        var httpResponse = await _client.PostAuthAsync(url, request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseEditTransactionPreviewJson>();
        payload.TransactionId.ShouldBe(transaction.Id);
        payload.Warnings.ShouldBeEmpty();

        payload.Impact.ReceivableImpact.BucketBefore.ShouldBe(AgingBucket.Days0To30);
        payload.Impact.ReceivableImpact.BucketAfter.ShouldBe(AgingBucket.Days31To60);
        payload.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        payload.Impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeFalse();

        payload.Impact.FiadoBalanceImpact.Deltas.Count.ShouldBe(2);
        var newClientDelta = payload.Impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == newClient.Id);
        newClientDelta.OutstandingDelta.ShouldBe(100m);
        newClientDelta.ClientName.ShouldBe("New Client");
        var oldClientDelta = payload.Impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == originalClient.Id);
        oldClientDelta.OutstandingDelta.ShouldBe(-100m);

        // No daily close seeded for the date → null close status, no current/projected variance.
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBeNull();
        payload.Impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
        payload.Impact.CashVarianceImpact.ProjectedVariance.ShouldBeNull();
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);

        // Preview never commits: every persisted scalar column is identical to the pre-call
        // state — not just the editable subset the payload tried to change. Navigation
        // properties are excluded because the reload does not load them. This is the real
        // "byte-for-byte unchanged" guard.
        var after = await factory.ReloadAsync<Transaction>(transaction.Id);
        after.ShouldNotBeNull();
        Scalars(after).ShouldBe(Scalars(before));
    }

    [Fact]
    public async Task EditPreview_ShouldReportDraftStatusAndWithholdVariance_WhenDraftCloseExistsForDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewOpenClose", Role.Admin);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa");
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var transactionDate = DateTime.UtcNow.Date;
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: terminal.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: transactionDate,
            value: 250m,
            dueDate: transactionDate,
            paidAt: null);
        await factory.SeedDailyCloseAsync(branch.Id, terminal.Id, transactionDate, DailyCloseStatus.Draft);

        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("touch")
            .WithDueDate(transactionDate.AddDays(1))
            .WithPaidAt(null)
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{transaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseEditTransactionPreviewJson>();
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Draft);
        payload.Impact.CashVarianceImpact.AccountId.ShouldBe(terminal.Id);
        payload.Impact.CashVarianceImpact.Date!.Value.Date.ShouldBe(transactionDate);
        // Draft close → counts still moving, so no current/projected variance is surfaced.
        payload.Impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
        payload.Impact.CashVarianceImpact.ProjectedVariance.ShouldBeNull();
        // §6.12 is blind to every editable field — the edit cannot move the variance.
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
    }

    [Fact]
    public async Task EditPreview_AllImpactPredictions_ShouldMatchPostUpdateReportsAndVariance()
    {
        // End-to-end determinism: each previewed impact must equal the real state once the identical
        // payload is committed via PUT /transaction/{id}.
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewDeterminism", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var originalClient = await factory.SeedClientAsync(branch.Id, "Original Determinism Client");
        var newClient = await factory.SeedClientAsync(branch.Id, "New Determinism Client");

        var asOfDate = new DateTime(2025, 6, 30);
        var transactionDate = new DateTime(2025, 3, 1);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var countedProduct = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(branch.Id, tabAccount.Id, transactionDate, DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(close.Id, countedProduct.Id, value: 500m);

        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: tabAccount.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: transactionDate,
            value: 100m,
            clientId: originalClient.Id,
            dueDate: transactionDate,
            paidAt: null);

        // Payload X: shift DueDate to 45 days before asOfDate (→ Days31To60), keep unpaid,
        // and move the outstanding balance from originalClient to newClient.
        var payloadX = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("determinism edit")
            .WithDueDate(asOfDate.AddDays(-45))
            .WithPaidAt(null)
            .WithClientId(newClient.Id)
            .WithTransactionTime(new TimeOnly(11, 0))
            .Build();

        // 1) Preview with payload X.
        var previewResponse = await _client.PostAuthAsync(
            $"/transaction/{transaction.Id}/edit-preview?asOfDate={asOfDate:yyyy-MM-dd}", payloadX, token);
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await previewResponse.ReadContentAsync<ResponseEditTransactionPreviewJson>();
        var predictedBucket = preview.Impact.ReceivableImpact.BucketAfter;
        predictedBucket.ShouldBe(AgingBucket.Days31To60);
        var originalClientDelta = preview.Impact.FiadoBalanceImpact.Deltas.Single(delta => delta.ClientId == originalClient.Id);
        var newClientDelta = preview.Impact.FiadoBalanceImpact.Deltas.Single(delta => delta.ClientId == newClient.Id);
        originalClientDelta.OutstandingDelta.ShouldBe(-100m);
        newClientDelta.OutstandingDelta.ShouldBe(100m);
        preview.Impact.CashVarianceImpact.CurrentVariance.ShouldBe(600m);
        preview.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
        preview.Impact.CashVarianceImpact.ProjectedVariance.ShouldBe(600m);

        var balanceBeforeResponse = await _client.GetAuthAsync($"/report/fiado/balance?asOfDate={asOfDate:yyyy-MM-dd}", token);
        balanceBeforeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var balanceBefore = await balanceBeforeResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        var originalBefore = BalanceFor(balanceBefore, originalClient.Id);
        var newBefore = BalanceFor(balanceBefore, newClient.Id);

        // 2) Commit the same payload X via the write twin.
        var updateResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", payloadX, token);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3) Receivable impact: read the affected row from the fiado aging report at the same asOfDate.
        var agingResponse = await _client.GetAuthAsync(
            $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50", token);
        agingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var aging = await agingResponse.ReadContentAsync<ResponseFiadoAgingJson>();

        var row = aging.Items.SingleOrDefault(item => item.TransactionId == transaction.Id);
        row.ShouldNotBeNull();
        row.Bucket.ShouldBe(predictedBucket!.Value);

        // 4) Fiado impact: the real before/after report deltas match the previewed deltas.
        var balanceAfterResponse = await _client.GetAuthAsync($"/report/fiado/balance?asOfDate={asOfDate:yyyy-MM-dd}", token);
        balanceAfterResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var balanceAfter = await balanceAfterResponse.ReadContentAsync<ResponseFiadoBalanceJson>();
        (BalanceFor(balanceAfter, originalClient.Id) - originalBefore).ShouldBe(originalClientDelta.OutstandingDelta);
        (BalanceFor(balanceAfter, newClient.Id) - newBefore).ShouldBe(newClientDelta.OutstandingDelta);

        // 5) Cash variance impact: the real recompute after the write equals the previewed projection.
        var actualVariance = await CalculateCashVarianceAsync(
            branch.Id,
            tabAccount.Id,
            transactionDate,
            close.Id);
        actualVariance.ShouldBe(preview.Impact.CashVarianceImpact.ProjectedVariance.GetValueOrDefault());
    }

    [Fact]
    public async Task EditPreview_ShouldLiveRecomputeCurrentVariance_WhenSubmittedCloseExistsForDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewSubmittedClose", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa");
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);

        var transactionDate = new DateTime(2025, 4, 10);

        // Submitted close: closing items 500 (excl. cv), no prior close → opening 0.
        var close = await factory.SeedDailyCloseAsync(branch.Id, terminal.Id, transactionDate, DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 500m);

        // Same-day ledger: In 100, Out 25 → net 75. §6.12 variance = 500 − 0 − 75 = 425.
        var inTransaction = await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.In,
            operatorContext.Id, user.Id, date: transactionDate, value: 100m, dueDate: transactionDate, paidAt: null);
        await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.Out,
            operatorContext.Id, user.Id, date: transactionDate, value: 25m);

        // Edit the In row (only DueDate moves — cannot touch §6.12).
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("touch")
            .WithDueDate(transactionDate.AddDays(1))
            .WithPaidAt(null)
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{inTransaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseEditTransactionPreviewJson>();
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Submitted);
        payload.Impact.CashVarianceImpact.AccountId.ShouldBe(terminal.Id);
        payload.Impact.CashVarianceImpact.CurrentVariance.ShouldBe(425m);
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
        payload.Impact.CashVarianceImpact.ProjectedVariance.ShouldBe(425m);
    }

    [Fact]
    public async Task EditPreview_ShouldLiveRecomputeCurrentVariance_WhenApprovedCloseExistsForDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewApprovedClose", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa");
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);

        var transactionDate = new DateTime(2025, 4, 10);

        // Approved (finalized) close: closing items 500 (excl. cv), no prior close → opening 0.
        var close = await factory.SeedDailyCloseAsync(branch.Id, terminal.Id, transactionDate, DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 500m);

        // Same-day ledger: In 100, Out 25 → net 75. §6.12 variance = 500 − 0 − 75 = 425.
        var inTransaction = await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.In,
            operatorContext.Id, user.Id, date: transactionDate, value: 100m, dueDate: transactionDate, paidAt: null);
        await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.Out,
            operatorContext.Id, user.Id, date: transactionDate, value: 25m);

        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("touch")
            .WithDueDate(transactionDate.AddDays(1))
            .WithPaidAt(null)
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{inTransaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseEditTransactionPreviewJson>();
        // Approved close is finalized → not "still open", but the signed-off variance is surfaced.
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Approved);
        payload.Impact.CashVarianceImpact.AccountId.ShouldBe(terminal.Id);
        payload.Impact.CashVarianceImpact.CurrentVariance.ShouldBe(425m);
        payload.Impact.CashVarianceImpact.ProjectedVariance.ShouldBe(425m);
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
    }

    [Fact]
    public async Task EditPreview_ShouldLiveRecomputeCurrentVariance_WhenRejectedCloseExistsForDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewRejectedClose", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa");
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);

        var transactionDate = new DateTime(2025, 4, 10);

        // Rejected close still holds its last-submitted counts (500, excl. cv); no prior close → opening 0.
        var close = await factory.SeedDailyCloseAsync(branch.Id, terminal.Id, transactionDate, DailyCloseStatus.Rejected);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 500m);

        // Same-day ledger: In 100, Out 25 → net 75. §6.12 variance = 500 − 0 − 75 = 425.
        var inTransaction = await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.In,
            operatorContext.Id, user.Id, date: transactionDate, value: 100m, dueDate: transactionDate, paidAt: null);
        await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.Out,
            operatorContext.Id, user.Id, date: transactionDate, value: 25m);

        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("touch")
            .WithDueDate(transactionDate.AddDays(1))
            .WithPaidAt(null)
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{inTransaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseEditTransactionPreviewJson>();
        // Rejected close is not "still open", but its last-submitted variance is surfaced (repudiated context).
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Rejected);
        payload.Impact.CashVarianceImpact.AccountId.ShouldBe(terminal.Id);
        payload.Impact.CashVarianceImpact.CurrentVariance.ShouldBe(425m);
        payload.Impact.CashVarianceImpact.ProjectedVariance.ShouldBe(425m);
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
    }

    // Every persisted scalar column on Transaction (navigation properties excluded). Anonymous
    // types have structural equality, so a regression that mutated any column during a preview
    // would fail the assertion without needing a source-level record whose properties look unused.
    private static object Scalars(Transaction transaction) => new
    {
        transaction.Id,
        transaction.CreatedAt,
        transaction.Active,
        transaction.Date,
        transaction.Value,
        transaction.Description,
        transaction.TransactionTime,
        transaction.TransactionTypeId,
        transaction.CategoryId,
        transaction.Direction,
        transaction.AccountId,
        transaction.ClientId,
        transaction.DueDate,
        transaction.PaidAt,
        transaction.OriginTransactionId,
        transaction.RecordedByOperatorId,
        transaction.CreatedByUserId,
        transaction.UpdatedAt,
        transaction.UpdatedByUserId,
        transaction.Status,
        transaction.CancelledAt,
        transaction.CancelledByUserId,
        transaction.CancellationReason,
        transaction.BranchId
    };

    private static decimal BalanceFor(ResponseFiadoBalanceJson payload, Guid clientId)
    {
        return payload.Items.SingleOrDefault(item => item.ClientId == clientId)?.OutstandingTotal ?? 0m;
    }

    private async Task<decimal> CalculateCashVarianceAsync(
        Guid branchId,
        Guid accountId,
        DateTime date,
        Guid dailyCloseId)
    {
        using var scope = factory.Services.CreateScope();
        var productResolver = scope.ServiceProvider.GetRequiredService<ICashVarianceProductResolver>();
        var calculator = scope.ServiceProvider.GetRequiredService<ICashVarianceCalculator>();
        var cashVarianceProductId = await productResolver.GetIdAsync(branchId);

        return await calculator.CalculateAsync(
            branchId,
            accountId,
            date,
            dailyCloseId,
            cashVarianceProductId,
            CancellationToken.None);
    }

}
