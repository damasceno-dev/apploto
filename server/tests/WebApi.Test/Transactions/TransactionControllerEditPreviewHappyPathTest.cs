using System.Net;
using CommonTestUtilities.Requests;
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
    public async Task EditPreview_BucketAfterPrediction_ShouldMatchPostUpdateAgingReport()
    {
        // End-to-end determinism: preview prediction must equal the real post-PUT state.
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewDeterminism", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Determinism Client");

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
            clientId: client.Id,
            dueDate: transactionDate,
            paidAt: null);

        // Payload X: shift DueDate to 45 days before asOfDate (→ Days31To60), keep unpaid.
        var payloadX = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("determinism edit")
            .WithDueDate(asOfDate.AddDays(-45))
            .WithPaidAt(null)
            .WithClientId(client.Id)
            .WithTransactionTime(new TimeOnly(11, 0))
            .Build();

        // 1) Preview with payload X.
        var previewResponse = await _client.PostAuthAsync(
            $"/transaction/{transaction.Id}/edit-preview?asOfDate={asOfDate:yyyy-MM-dd}", payloadX, token);
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await previewResponse.ReadContentAsync<ResponseEditTransactionPreviewJson>();
        var predictedBucket = preview.Impact.ReceivableImpact.BucketAfter;
        predictedBucket.ShouldBe(AgingBucket.Days31To60);

        // 2) Commit the same payload X via the write twin.
        var updateResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", payloadX, token);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3) Read the affected row from the fiado aging report at the same asOfDate.
        var agingResponse = await _client.GetAuthAsync(
            $"/report/fiado/aging?asOfDate={asOfDate:yyyy-MM-dd}&page=1&pageSize=50", token);
        agingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var aging = await agingResponse.ReadContentAsync<ResponseFiadoAgingJson>();

        var row = aging.Items.SingleOrDefault(item => item.TransactionId == transaction.Id);
        row.ShouldNotBeNull();
        row.Bucket.ShouldBe(predictedBucket!.Value);
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

    // Every persisted scalar column on Transaction (navigation properties excluded). Value
    // equality on the record makes the reload comparison exhaustive, so a regression that
    // mutated any column during a preview would fail the assertion.
    private static TransactionScalars Scalars(Transaction transaction) => new(
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
        transaction.BranchId);

    private sealed record TransactionScalars(
        Guid Id,
        DateTime CreatedAt,
        bool Active,
        DateTime Date,
        decimal Value,
        string? Description,
        TimeOnly? TransactionTime,
        Guid TransactionTypeId,
        Guid CategoryId,
        Direction Direction,
        Guid AccountId,
        Guid? ClientId,
        DateTime DueDate,
        DateTime? PaidAt,
        Guid? OriginTransactionId,
        Guid RecordedByOperatorId,
        Guid CreatedByUserId,
        DateTime? UpdatedAt,
        Guid? UpdatedByUserId,
        TransactionStatus Status,
        DateTime? CancelledAt,
        Guid? CancelledByUserId,
        string? CancellationReason,
        Guid BranchId);
}
