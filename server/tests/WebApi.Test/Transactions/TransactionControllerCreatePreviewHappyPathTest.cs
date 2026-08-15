using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerCreatePreviewHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreatePreview_ShouldReturn200WithImpactShape_AndPersistNothing()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewHappy", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);
        var client = await factory.SeedClientAsync(branch.Id, "Preview Client");

        var asOfDate = new DateTime(2025, 6, 30);
        var transactionDate = new DateTime(2025, 5, 16); // 45 days before as-of → Days31To60
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(transactionDate)
            .WithValue(200m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(tabAccount.Id)
            .WithClientId(client.Id)
            .WithRecordedByOperatorId(operatorContext.Id)
            .Build();

        var countBefore = await CountBranchTransactionsAsync(branch.Id);

        var url = $"/transaction/preview?asOfDate={asOfDate:yyyy-MM-dd}";
        var httpResponse = await _client.PostAuthAsync(url, request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionPreviewJson>();
        payload.Warnings.ShouldBeEmpty();

        // Receivable: a fresh unpaid Tab row appears in open receivables at Days31To60.
        payload.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeTrue();
        payload.Impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeFalse();
        payload.Impact.ReceivableImpact.BucketBefore.ShouldBeNull();
        payload.Impact.ReceivableImpact.BucketAfter.ShouldBe(AgingBucket.Days31To60);

        // Fiado: §6.4 Out raises outstanding (+200) on the selected client.
        payload.Impact.FiadoBalanceImpact.Deltas.Count.ShouldBe(1);
        var delta = payload.Impact.FiadoBalanceImpact.Deltas.Single();
        delta.ClientId.ShouldBe(client.Id);
        delta.ClientName.ShouldBe("Preview Client");
        delta.OutstandingDelta.ShouldBe(200m);

        // Cash variance: the new Out row moves the day's variance by +200 (no close → no current number).
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(200m);
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBeNull();
        payload.Impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
        payload.Impact.CashVarianceImpact.ProjectedVariance.ShouldBeNull();

        // Preview never commits: the branch's transaction count is unchanged.
        var countAfter = await CountBranchTransactionsAsync(branch.Id);
        countAfter.ShouldBe(countBefore);
        countAfter.ShouldBe(0);
    }

    [Fact]
    public async Task CreatePreview_ShouldForecastCashVariance_AgainstSeededSubmittedClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewSubmitted", Role.Admin);
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

        // Existing same-day ledger: In 100, Out 25 → net 75 → §6.12 variance = 500 − 0 − 75 = 425.
        await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.In,
            operatorContext.Id, user.Id, date: transactionDate, value: 100m, dueDate: transactionDate, paidAt: null);
        await factory.SeedTransactionAsync(
            branch.Id, terminal.Id, transactionType.Id, category.Id, Direction.Out,
            operatorContext.Id, user.Id, date: transactionDate, value: 25m);

        // Preview recording a NEW In 100 on the same (account, date): −NetFlow(In, 100) = −100.
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(transactionDate)
            .WithValue(100m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(terminal.Id)
            .WithRecordedByOperatorId(operatorContext.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionPreviewJson>();
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Submitted);
        payload.Impact.CashVarianceImpact.AccountId.ShouldBe(terminal.Id);
        payload.Impact.CashVarianceImpact.Date!.Value.Date.ShouldBe(transactionDate);
        payload.Impact.CashVarianceImpact.CurrentVariance.ShouldBe(425m);
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(-100m);
        payload.Impact.CashVarianceImpact.ProjectedVariance.ShouldBe(325m);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn200_WhenMemberActsOnLinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewMember", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(new DateTime(2025, 5, 16))
            .WithValue(75m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionPreviewJson>();
        // Terminal In create → no receivable/fiado, variance delta = −75.
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(-75m);
        payload.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        payload.Impact.FiadoBalanceImpact.Deltas.ShouldBeEmpty();

        (await CountBranchTransactionsAsync(branch.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn200_WhenSaveAsDraft_AllSectionsEmpty()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewDraft", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Draft Client");

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(new DateTime(2025, 5, 16))
            .WithValue(200m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(tabAccount.Id)
            .WithClientId(client.Id)
            .WithRecordedByOperatorId(operatorContext.Id)
            .WithSaveAsDraft(true)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionPreviewJson>();
        // A Draft would-be row is invisible to every Active-filtered surface.
        payload.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        payload.Impact.ReceivableImpact.BucketAfter.ShouldBeNull();
        payload.Impact.FiadoBalanceImpact.Deltas.ShouldBeEmpty();
        payload.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
        payload.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBeNull();
    }

    [Fact]
    public async Task CreatePreview_AllImpactPredictions_ShouldRemainHypothetical_WhenSubmittedCloseFreezesWrite()
    {
        // End-to-end determinism: each previewed impact must equal the real state once the identical
        // payload is committed via POST /transaction.
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewDeterminism", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tabAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.SameDay,
            requiresTabAccountAndClient: true);
        var client = await factory.SeedClientAsync(branch.Id, "Determinism Client");

        var asOfDate = new DateTime(2025, 6, 30);
        var transactionDate = new DateTime(2025, 5, 16); // 45 days before as-of → Days31To60

        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var countedProduct = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(branch.Id, tabAccount.Id, transactionDate, DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(close.Id, countedProduct.Id, value: 500m);

        // Payload X: a Tab fiado create.
        var payloadX = new RequestCreateTransactionJsonBuilder()
            .WithDate(transactionDate)
            .WithValue(150m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(tabAccount.Id)
            .WithClientId(client.Id)
            .WithRecordedByOperatorId(operatorContext.Id)
            .Build();

        // 1) Preview with payload X.
        var previewResponse = await _client.PostAuthAsync(
            $"/transaction/preview?asOfDate={asOfDate:yyyy-MM-dd}", payloadX, token);
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await previewResponse.ReadContentAsync<ResponseCreateTransactionPreviewJson>();
        var predictedBucket = preview.Impact.ReceivableImpact.BucketAfter;
        predictedBucket.ShouldBe(AgingBucket.Days31To60);
        var predictedFiadoDelta = preview.Impact.FiadoBalanceImpact.Deltas.Single();
        predictedFiadoDelta.ClientId.ShouldBe(client.Id);
        predictedFiadoDelta.OutstandingDelta.ShouldBe(150m);
        preview.Impact.CashVarianceImpact.CurrentVariance.ShouldBe(500m);
        preview.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(150m);
        preview.Impact.CashVarianceImpact.ProjectedVariance.ShouldBe(650m);

        // 2) The identical write twin is frozen once the close is Submitted. Preview remains
        // a hypothetical divergence display; it does not authorize a stale ledger mutation.
        var createResponse = await _client.PostAuthAsync("/transaction", payloadX, token);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await createResponse.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        (await CountBranchTransactionsAsync(branch.Id)).ShouldBe(0);

        // 3) No write occurred, so a diagnostic recompute still equals the frozen current snapshot.
        var actualVariance = await CalculateCashVarianceAsync(
            branch.Id,
            tabAccount.Id,
            transactionDate,
            close.Id);
        actualVariance.ShouldBe(preview.Impact.CashVarianceImpact.CurrentVariance.GetValueOrDefault());
    }

    private async Task<int> CountBranchTransactionsAsync(Guid branchId)
    {
        using var scope = factory.Services.CreateScope();
        var transactionsRepository = scope.ServiceProvider.GetRequiredService<ITransactionsRepository>();
        return await transactionsRepository.CountByBranchIdAsNoTracking(
            branchId,
            new TransactionListFilter { Page = 1, PageSize = 100 });
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
