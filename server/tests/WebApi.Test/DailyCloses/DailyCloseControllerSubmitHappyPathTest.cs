using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerSubmitHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Submit_ShouldReturn200AndPersistSubmittedStateAuditAndCashVariance_WhenMemberSubmitsDraft()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitMemberDraft", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Draft,
            submittedByOperatorId: op.Id);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 500m);
        await SeedTransactionAsync(branch.Id, account.Id, op.Id, user.Id, close.Date, Direction.In, 100m);
        await SeedTransactionAsync(branch.Id, account.Id, op.Id, user.Id, close.Date, Direction.Out, 25m);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await httpResponse.Content.ReadAsStringAsync());
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Submitted);
        payload.SubmittedAt.ShouldNotBeNull();
        payload.UpdatedAt.ShouldNotBeNull();
        payload.SubmittedAt.ShouldBe(payload.UpdatedAt);
        payload.SubmittedByOperatorId.ShouldBe(op.Id);
        payload.Items.ShouldContain(item => item.ProductId == cvProduct.Id && item.Value == 425m);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.SubmittedAt.ShouldNotBeNull();
        persisted.UpdatedAt.ShouldNotBeNull();
        persisted.SubmittedAt.ShouldBe(persisted.UpdatedAt);
        persisted.SubmittedByOperatorId.ShouldBe(op.Id);

        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        var cashVariance = items.Single(item => item.ProductId == cvProduct.Id);
        cashVariance.Value.ShouldBe(425m);
        cashVariance.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_ShouldUseOnlyRequestedAccount_WhenSiblingAccountHasDifferentLedger()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitSibling", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var siblingAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Draft,
            submittedByOperatorId: op.Id);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 100m);

        await SeedTransactionAsync(branch.Id, account.Id, op.Id, user.Id, close.Date, Direction.In, 20m);
        await SeedTransactionAsync(branch.Id, siblingAccount.Id, op.Id, user.Id, close.Date, Direction.In, 9_000m);
        await SeedTransactionAsync(branch.Id, siblingAccount.Id, op.Id, user.Id, close.Date, Direction.Out, 4_000m);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        items.Single(item => item.ProductId == cvProduct.Id).Value.ShouldBe(80m);
    }

    [Fact]
    public async Task Submit_ShouldMutateExistingCashVarianceItemInPlace_WhenResubmittingRejectedClose()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitRejected", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday().AddDays(-1),
            status: DailyCloseStatus.Rejected);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 400m);
        var existingCashVariance = await factory.SeedDailyCloseItemAsync(close.Id, cvProduct.Id, value: 1m);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        var cashVariance = items.Single(item => item.ProductId == cvProduct.Id);
        cashVariance.Id.ShouldBe(existingCashVariance.Id);
        cashVariance.Value.ShouldBe(400m);
        cashVariance.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_ShouldPreserveCashVarianceItemId_WhenSubmittedCloseIsRecalledAndSubmittedAgain()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitRecallCycle", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var openRequest = new RequestOpenDailyCloseJson
        {
            AccountId = account.Id,
            Date = LocalToday()
        };

        var openResponse = await _client.PostAuthAsync("/dailyclose", openRequest, token);
        openResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var opened = await openResponse.ReadContentAsync<ResponseDailyCloseJson>();

        await PutItemsAsync(opened.Id, product.Id, 100m, token);
        var firstSubmit = await _client.PostAuthAsync($"/dailyclose/{opened.Id}/submit", token);
        firstSubmit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstCashVariance = (await factory.ListDailyCloseItemsByDailyCloseIdAsync(opened.Id))
            .Single(item => item.ProductId == cvProduct.Id);
        firstCashVariance.Value.ShouldBe(100m);

        var recall = await _client.PostAuthAsync($"/dailyclose/{opened.Id}/recall", token);
        recall.StatusCode.ShouldBe(HttpStatusCode.OK);
        await PutItemsAsync(opened.Id, product.Id, 150m, token);
        var secondSubmit = await _client.PostAuthAsync($"/dailyclose/{opened.Id}/submit", token);

        secondSubmit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondCashVariance = (await factory.ListDailyCloseItemsByDailyCloseIdAsync(opened.Id))
            .Single(item => item.ProductId == cvProduct.Id);
        secondCashVariance.Id.ShouldBe(firstCashVariance.Id);
        secondCashVariance.Value.ShouldBe(150m);
    }

    [Fact]
    public async Task Submit_ShouldUseOpeningBalanceFromMostRecentPriorClose_WhenBranchHasSkippedDays()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitPriorLookback", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var closeDate = LocalToday();

        var oldestClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate.AddDays(-5),
            status: DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(oldestClose.Id, product.Id, value: 100m);

        var middleClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate.AddDays(-3),
            status: DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(middleClose.Id, product.Id, value: 200m);

        var mostRecentClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate.AddDays(-1),
            status: DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(mostRecentClose.Id, product.Id, value: 300m);

        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate,
            status: DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 500m);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        items.Single(item => item.ProductId == cvProduct.Id).Value.ShouldBe(200m);
    }

    [Fact]
    public async Task Submit_ShouldIgnoreCashVarianceProduct_WhenCalculatingOpeningBalance()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitPriorCvExclusion", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var closeDate = LocalToday();
        var priorClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate.AddDays(-1),
            status: DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(priorClose.Id, product.Id, value: 100m);
        await factory.SeedDailyCloseItemAsync(priorClose.Id, cvProduct.Id, value: 9_999m);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate,
            status: DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 200m);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        items.Single(item => item.ProductId == cvProduct.Id).Value.ShouldBe(100m);
    }

    [Fact]
    public async Task Submit_ShouldReturn200_WhenManagerSubmitsOlderDayClose()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitMgrOlder", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday().AddDays(-5),
            status: DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 90m);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        items.Single(item => item.ProductId == cvProduct.Id).Value.ShouldBe(90m);
    }

    [Fact]
    public async Task Submit_ShouldIgnoreOutstandingDraftsOutsideExactAccountDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcSubmitDraftTupleIsolation",
            Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var siblingAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var date = LocalToday();
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, 100m);
        await factory.SeedTransactionAsync(
            branch.Id,
            siblingAccount.Id,
            transactionType.Id,
            category.Id,
            Direction.In,
            op.Id,
            user.Id,
            date,
            status: TransactionStatus.Draft);
        await factory.SeedTransactionAsync(
            branch.Id,
            account.Id,
            transactionType.Id,
            category.Id,
            Direction.In,
            op.Id,
            user.Id,
            date.AddDays(-1),
            status: TransactionStatus.Draft);

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submitted = await response.ReadContentAsync<ResponseDailyCloseJson>();
        submitted.Status.ShouldBe(DailyCloseStatus.Submitted);
        submitted.Items.Single(item => item.ProductId == cashVarianceProduct.Id).Value.ShouldBe(100m);
    }

    /// <summary>
    /// Access-faithful end-to-end day pinning the §6.12 sign convention: opening counts
    /// R$5,800 (cash 5,000 + stock 800), the day records R$3,000 of Out rows (cash-to-bank
    /// deposit 2,000, PIX cash-out 800, payout 200), the operator counts R$2,780 — the
    /// persisted Diferença Caixa is −R$20 (the drawer came up R$20 short).
    /// </summary>
    [Fact]
    public async Task Submit_ShouldPersistMinus20Variance_ForRealisticAllOutTerminalDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitRealisticDay", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var cash = await factory.SeedProductAsync(branch.Id);
        var telesena = await factory.SeedProductAsync(branch.Id);
        var raspadinha = await factory.SeedProductAsync(branch.Id);
        var closeDate = LocalToday();
        var priorClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate.AddDays(-1),
            status: DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(priorClose.Id, cash.Id, value: 5_000m);
        await factory.SeedDailyCloseItemAsync(priorClose.Id, telesena.Id, value: 500m);
        await factory.SeedDailyCloseItemAsync(priorClose.Id, raspadinha.Id, value: 300m);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate,
            status: DailyCloseStatus.Draft,
            submittedByOperatorId: op.Id);
        await factory.SeedDailyCloseItemAsync(close.Id, cash.Id, value: 1_980m);
        await factory.SeedDailyCloseItemAsync(close.Id, telesena.Id, value: 500m);
        await factory.SeedDailyCloseItemAsync(close.Id, raspadinha.Id, value: 300m);
        await SeedTransactionAsync(branch.Id, account.Id, op.Id, user.Id, close.Date, Direction.Out, 2_000m);
        await SeedTransactionAsync(branch.Id, account.Id, op.Id, user.Id, close.Date, Direction.Out, 800m);
        await SeedTransactionAsync(branch.Id, account.Id, op.Id, user.Id, close.Date, Direction.Out, 200m);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        items.Single(item => item.ProductId == cvProduct.Id).Value.ShouldBe(-20m);
    }

    private async Task PutItemsAsync(Guid closeId, Guid productId, decimal value, string token)
    {
        var close = await factory.ReloadAsync<DailyClose>(closeId);
        close.ShouldNotBeNull();
        var request = new RequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = productId, Value = value }]
        };

        var response = await _client.PutAuthAsync($"/dailyclose/{closeId}/items", request, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SeedTransactionAsync(
        Guid branchId,
        Guid accountId,
        Guid operatorId,
        Guid userId,
        DateTime date,
        Direction direction,
        decimal value)
    {
        var category = await factory.SeedCategoryAsync(branchId, defaultDirection: direction);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        await factory.SeedTransactionAsync(
            branchId,
            accountId,
            transactionType.Id,
            category.Id,
            direction,
            operatorId,
            userId,
            date: date,
            value: value,
            status: TransactionStatus.Active);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
