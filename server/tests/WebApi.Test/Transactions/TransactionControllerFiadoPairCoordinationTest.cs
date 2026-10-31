using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 8 audit of the Phase 5.6 physical pairs against the paired terminal's session, under the M9.6 Phase 4
/// Conferir gate. The count is confirmed first; Submit and the physical operation are then queued behind the held
/// cashbox key and released in a known grant order (no timing sleeps). An operation granted first changes the ledger
/// revision, so the queued Submit refuses the stale attempt and the unchanged count is re-confirmed against the new
/// ledger before it submits; a Submit granted first freezes the session and the queued operation writes neither leg.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class TransactionControllerFiadoPairCoordinationTest(ServerWebApplicationFactory factory)
{
    private static readonly TimeSpan GrantOrderTimeout = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(PhysicalOperation.CountedStockSale)]
    [InlineData(PhysicalOperation.CashRepayment)]
    public async Task OperationGrantedFirst_ShouldInvalidateTheConfirmedAttempt_BeforeTheQueuedSubmit(PhysicalOperation operation)
    {
        var (ctx, products) = await SeedCountedDayAsync($"FiadoPairFirst{operation}", operation);
        var client = factory.CreateClient();
        var confirmed = (await client.ConferAsync(ctx.CloseId, ctx.Token)).Session;
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var operationClient = grantOrderFactory.CreateClient();
        using var submitClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldAccountKeyAsync(ctx.BranchId, ctx.TerminalId);

        var operationTask = Send(operationClient, ctx, operation);
        await heldLock.WaitForWaitersAsync(1);
        var submitTask = submitClient.PostSubmitAsync(confirmed, ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(operationTask, submitTask).WaitAsync(TimeSpan.FromSeconds(15));
        var operationResponse = await operationTask;
        var submit = await submitTask;

        operationResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await operationResponse.Content.ReadAsStringAsync());
        submit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await submit.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldBe([ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT]);
        (await factory.ReloadAsync<DailyClose>(ctx.CloseId))!.Status.ShouldBe(DailyCloseStatus.Draft);

        // The count already describes the cashbox after the movement: the unconfirmed recalculation is zero, and the
        // operator confirms the unchanged count against the new ledger instead of recounting.
        var unconfirmed = (await client.TryGetSessionAsync(ctx.CloseId, ctx.Token))!.Count!;
        unconfirmed.CandidateState.ShouldBe(SessionCandidateState.Unconfirmed);
        unconfirmed.ConfirmedAttempt!.Difference.ShouldBe(operation == PhysicalOperation.CountedStockSale ? -5m : 5m);
        unconfirmed.UnconfirmedRecalculation!.Difference.ShouldBe(0m);
        var reconfirmed = await client.ConferAsync(ctx.CloseId, ctx.Token, confirmStillObserved: true);
        reconfirmed.Attempt.Kind.ShouldBe(SessionCountAttemptKind.StillObserved);
        var resubmit = await client.PostSubmitAsync(reconfirmed.Session, ctx.Token);
        resubmit.StatusCode.ShouldBe(HttpStatusCode.OK, await resubmit.Content.ReadAsStringAsync());
        var rows = await ctx.ListBranchTransactionsAsync();
        rows.Count.ShouldBe(2);
        rows.Count(row => row.GenerationKind is not null).ShouldBe(1);
        // The count already reflects the physical movement (995 stock / 1,005 cash); with the generated leg in the
        // Submit's inputs the official variance is zero.
        (await OfficialVariance(ctx, products)).ShouldBe(0m);
    }

    [Theory]
    [InlineData(PhysicalOperation.CountedStockSale)]
    [InlineData(PhysicalOperation.CashRepayment)]
    public async Task SubmitGrantedFirst_ShouldFreezeTheDay_AndTheQueuedOperationWritesNeitherLeg(PhysicalOperation operation)
    {
        var (ctx, products) = await SeedCountedDayAsync($"FiadoSubmitFirst{operation}", operation);
        var confirmed = (await factory.CreateClient().ConferAsync(ctx.CloseId, ctx.Token)).Session;
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var submitClient = grantOrderFactory.CreateClient();
        using var operationClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldAccountKeyAsync(ctx.BranchId, ctx.TerminalId);

        var submitTask = submitClient.PostSubmitAsync(confirmed, ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var operationTask = Send(operationClient, ctx, operation);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(submitTask, operationTask).WaitAsync(TimeSpan.FromSeconds(15));
        var submit = await submitTask;
        var operationResponse = await operationTask;

        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        operationResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await operationResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        (await ctx.ListBranchTransactionsAsync()).ShouldBeEmpty();
        // Submit saw no movement: the counted 995 / 1,005 is officially a −5 / +5 difference, never rewritten.
        (await OfficialVariance(ctx, products)).ShouldBe(operation == PhysicalOperation.CountedStockSale ? -5m : 5m);
    }

    [Theory]
    [InlineData(PhysicalOperation.CountedStockSale)]
    [InlineData(PhysicalOperation.CashRepayment)]
    public async Task PhysicalOperation_ShouldWriteNeitherLeg_OnALockedDate(PhysicalOperation operation)
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, $"FiadoPairLocked{operation}", Role.Manager);
        await factory.SeedSettingAsync(ctx.BranchId, lockDate: ctx.Today);

        var response = await Send(factory.CreateClient(), ctx, operation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        (await ctx.ListBranchTransactionsAsync()).ShouldBeEmpty();
    }

    public enum PhysicalOperation
    {
        CountedStockSale,
        CashRepayment
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, FiadoLinkedMovementTestContext ctx, PhysicalOperation operation)
    {
        return operation == PhysicalOperation.CountedStockSale
            ? client.PostAuthAsync("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 5m, handsOutCountedStock: true), ctx.Token)
            : client.PostAuthAsync("/transaction/fiado-payment", TransactionControllerFiadoLinkedMovementTest.Payment(ctx, ctx.CashMethodId, 5m), ctx.Token);
    }

    /// <summary>
    /// A paired Terminal with yesterday's counted close as the opening (stock 1,000, cash 1,000) and today's Draft
    /// close already counted as the physical operation would leave it.
    /// </summary>
    private async Task<(FiadoLinkedMovementTestContext Ctx, CountedProducts Products)> SeedCountedDayAsync(
        string label,
        PhysicalOperation operation)
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, label, Role.Manager);
        var stock = await factory.SeedProductAsync(ctx.BranchId, "Raspadinha", displayOrder: 1);
        var cash = await factory.SeedProductAsync(ctx.BranchId, "Dinheiro", displayOrder: 2);
        var variance = await factory.SeedProductAsync(ctx.BranchId, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        var yesterday = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.TerminalId, ctx.Today.AddDays(-1), DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(yesterday.Id, stock.Id, 1000m);
        await factory.SeedDailyCloseItemAsync(yesterday.Id, cash.Id, 1000m);

        var close = (await factory.ReloadAsync<DailyClose>(ctx.CloseId))!;
        var counted = operation == PhysicalOperation.CountedStockSale ? (Stock: 995m, Cash: 1000m) : (Stock: 1000m, Cash: 1005m);
        var put = await factory.CreateClient().PutAuthAsync(
            $"/session/{ctx.CloseId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items =
                [
                    new RequestUpsertDailyCloseItemJson { ProductId = stock.Id, Value = counted.Stock },
                    new RequestUpsertDailyCloseItemJson { ProductId = cash.Id, Value = counted.Cash }
                ]
            },
            ctx.Token);
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        return (ctx, new CountedProducts(variance.Id));
    }

    private async Task<decimal> OfficialVariance(FiadoLinkedMovementTestContext ctx, CountedProducts products)
    {
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(ctx.CloseId);
        return items.Single(item => item.ProductId == products.Variance).Value;
    }

    private WebApplicationFactory<Program> CreateGrantOrderFactory()
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DailyCloseAccountCoordinationOptions>();
            services.AddSingleton(new DailyCloseAccountCoordinationOptions(GrantOrderTimeout));
        }));
    }

    private async Task<HeldAdvisoryLock> HoldAccountKeyAsync(Guid branchId, Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        return await HeldAdvisoryLock.AcquireAsync(connectionString, DailyCloseAccountCoordinationKey.Compute(branchId, accountId));
    }

    private sealed record CountedProducts(Guid Variance);
}
