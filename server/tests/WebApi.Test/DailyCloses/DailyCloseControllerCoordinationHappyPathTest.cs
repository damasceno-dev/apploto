using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerCoordinationHappyPathTest(
    ServerWebApplicationFactory factory,
    ITestOutputHelper output)
{
    private static readonly TimeSpan GrantOrderLockTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Coordination_ShouldMakeFirstScopedMemberClaimWin_WhenTwoMembersCountUnclaimedClose()
    {
        var (_, branch, _, openerToken) = await factory.SeedFullBranchContextAsync(
            "DcCoordFirstClaim",
            Role.Manager);
        var firstUser = await factory.SeedUserAsync();
        var firstMembership = await factory.SeedBranchUserAsync(firstUser.Id, branch.Id, Role.Member);
        var firstToken = factory.IssueBranchToken(firstMembership);
        var firstOperator = await factory.SeedOperatorAsync(branch.Id, userId: firstUser.Id);
        var secondUser = await factory.SeedUserAsync();
        var secondMembership = await factory.SeedBranchUserAsync(secondUser.Id, branch.Id, Role.Member);
        var secondToken = factory.IssueBranchToken(secondMembership);
        var secondOperator = await factory.SeedOperatorAsync(branch.Id, userId: secondUser.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(firstOperator.Id, account.Id);
        await factory.SeedOperatorAccountAsync(secondOperator.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        using var openerClient = factory.CreateClient();
        var openResponse = await openerClient.PostAuthAsync(
            "/dailyclose",
            new RequestOpenDailyCloseJson
            {
                AccountId = account.Id,
                Date = LocalToday().AddDays(-3)
            },
            openerToken);
        openResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var opened = await openResponse.ReadContentAsync<ResponseDailyCloseJson>();

        await using var heldLock = await HoldCoordinationLockAsync(branch.Id, account.Id);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var firstClient = grantOrderFactory.CreateClient();
        using var secondClient = grantOrderFactory.CreateClient();
        var firstTask = firstClient.PutAuthAsync(
            $"/dailyclose/{opened.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = opened.Version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }]
            },
            firstToken);
        await heldLock.WaitForWaitersAsync(1);
        var secondTask = secondClient.PutAuthAsync(
            $"/dailyclose/{opened.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = opened.Version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 20m }]
            },
            secondToken);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        var firstResponse = await firstTask;
        var secondResponse = await secondTask;

        firstResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await firstResponse.Content.ReadAsStringAsync());
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await secondResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
        var persisted = await factory.ReloadAsync<DailyClose>(opened.Id);
        persisted.ShouldNotBeNull();
        persisted.RecordedByUserId.ShouldBe(firstUser.Id);
        persisted.RecordedByOperatorId.ShouldBe(firstOperator.Id);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(opened.Id);
        items.ShouldHaveSingleItem().Value.ShouldBe(10m);
    }

    [Fact]
    public async Task Coordination_ShouldPreventSilentlyStaleSubmittedVariance_WhenCreateRacesSubmit()
    {
        var context = await SeedRaceContextAsync("DcCoordSubmitRace", DailyCloseStatus.Draft);
        await using var heldLock = await HoldCoordinationLockAsync(
            context.BranchId,
            context.AccountId);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var mutationClient = grantOrderFactory.CreateClient();
        using var workflowClient = grantOrderFactory.CreateClient();
        var mutationTask = mutationClient.PostAuthAsync(
            "/transaction",
            BuildCreateRequest(context, 25m),
            context.Token);
        await heldLock.WaitForWaitersAsync(1);
        var submitTask = workflowClient.PostAuthAsync(
            $"/dailyclose/{context.CloseId}/submit",
            context.Token);

        await heldLock.WaitForWaitersAsync(2);
        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        var mutationResponse = await mutationTask;
        var submitResponse = await submitTask;

        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        mutationResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var persistedClose = await factory.ReloadAsync<DailyClose>(context.CloseId);
        persistedClose.ShouldNotBeNull();
        persistedClose.Status.ShouldBe(DailyCloseStatus.Submitted);
        persistedClose.SubmittedAt.ShouldNotBeNull();
        var persistedItems = await factory.ListDailyCloseItemsByDailyCloseIdAsync(context.CloseId);
        var variance = persistedItems.Single(item => item.ProductId == context.CashVarianceProductId);
        variance.Value.ShouldBe(75m);
    }

    [Fact]
    public async Task Coordination_ShouldRejectMutationAndPreserveSnapshot_WhenCreateRacesApprove()
    {
        var context = await SeedRaceContextAsync("DcCoordApproveRace", DailyCloseStatus.Submitted);
        await using var heldLock = await HoldCoordinationLockAsync(
            context.BranchId,
            context.AccountId);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var mutationClient = grantOrderFactory.CreateClient();
        using var workflowClient = grantOrderFactory.CreateClient();
        var mutationTask = mutationClient.PostAuthAsync(
            "/transaction",
            BuildCreateRequest(context, 25m),
            context.Token);
        await heldLock.WaitForWaitersAsync(1);
        var approveTask = workflowClient.PostAuthAsync(
            $"/dailyclose/{context.CloseId}/approve",
            context.Token);

        await heldLock.WaitForWaitersAsync(2);
        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        var mutationResponse = await mutationTask;
        var approveResponse = await approveTask;

        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        mutationResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await mutationResponse.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);

        var persistedClose = await factory.ReloadAsync<DailyClose>(context.CloseId);
        persistedClose.ShouldNotBeNull();
        persistedClose.Status.ShouldBe(DailyCloseStatus.Approved);
        persistedClose.ApprovedAt.ShouldNotBeNull();
        var persistedItems = await factory.ListDailyCloseItemsByDailyCloseIdAsync(context.CloseId);
        persistedItems.Single(item => item.ProductId == context.CashVarianceProductId)
            .Value.ShouldBe(100m);
    }

    [Fact]
    public async Task Coordination_ShouldFreezeQueuedMutation_WhenSubmitWinsTheGrantOrder()
    {
        var context = await SeedRaceContextAsync("DcCoordSubmitFirstRace", DailyCloseStatus.Draft);
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var submitClient = grantOrderFactory.CreateClient();
        using var mutationClient = grantOrderFactory.CreateClient();
        var submitTask = submitClient.PostAuthAsync(
            $"/dailyclose/{context.CloseId}/submit",
            context.Token);
        await heldLock.WaitForWaitersAsync(1);
        var mutationTask = mutationClient.PostAuthAsync(
            "/transaction",
            BuildCreateRequest(context, 25m),
            context.Token);

        await heldLock.WaitForWaitersAsync(2);
        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        var submitResponse = await submitTask;
        var mutationResponse = await mutationTask;

        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        mutationResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await mutationResponse.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(context.CloseId);
        items.Single(item => item.ProductId == context.CashVarianceProductId).Value.ShouldBe(100m);
        (await CountTransactionsAsync(context)).ShouldBe(0);
    }

    [Fact]
    public async Task Coordination_ShouldReturnRetryableConflictAfterFiveSecondLockTimeout_ThenRetryCleanly()
    {
        var context = await SeedRaceContextAsync("DcCoordTimeout", DailyCloseStatus.Draft);
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var client = factory.CreateClient();
        var startedAt = DateTime.UtcNow;
        var blockedTask = client.PostAuthAsync(
            "/transaction",
            BuildCreateRequest(context, 25m),
            context.Token);
        await heldLock.WaitForWaitersAsync(1);

        var blockedResponse = await blockedTask;
        var elapsed = DateTime.UtcNow - startedAt;

        blockedResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await blockedResponse.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY);
        elapsed.ShouldBeGreaterThan(TimeSpan.FromSeconds(4));
        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(8));
        (await CountTransactionsAsync(context)).ShouldBe(0);

        await heldLock.ReleaseAsync();
        var retry = await client.PostAuthAsync(
            "/transaction",
            BuildCreateRequest(context, 25m),
            context.Token);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await CountTransactionsAsync(context)).ShouldBe(1);
    }

    [Fact]
    public async Task Coordination_ShouldTranslateWrappedSaveLockTimeout_WithoutPartialSubmit_ThenRetryCleanly()
    {
        var context = await SeedRaceContextAsync("DcCoordWrappedTimeout", DailyCloseStatus.Draft);
        await using var heldRowLock = await HoldDailyCloseRowLockAsync(context.CloseId);
        using var client = factory.CreateClient();

        var blockedResponse = await client.PostAuthAsync(
            $"/dailyclose/{context.CloseId}/submit",
            context.Token);

        blockedResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await blockedResponse.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY);
        var unchanged = await factory.ReloadAsync<DailyClose>(context.CloseId);
        unchanged.ShouldNotBeNull();
        unchanged.Status.ShouldBe(DailyCloseStatus.Draft);
        unchanged.SubmittedAt.ShouldBeNull();
        var itemsBeforeRetry = await factory.ListDailyCloseItemsByDailyCloseIdAsync(context.CloseId);
        itemsBeforeRetry.ShouldNotContain(item => item.ProductId == context.CashVarianceProductId);

        await heldRowLock.ReleaseAsync();
        var retry = await client.PostAuthAsync(
            $"/dailyclose/{context.CloseId}/submit",
            context.Token);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<DailyClose>(context.CloseId))!.Status.ShouldBe(DailyCloseStatus.Submitted);
    }

    [Fact]
    public async Task Coordination_ShouldEmitMeasuredWaitAndHoldDurations_ForARealRequestTransaction()
    {
        var context = await SeedRaceContextAsync("DcCoordTiming", DailyCloseStatus.Draft);
        var timingSink = new CoordinationTimingLogSink();
        using var timingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(timingSink)));
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var client = timingFactory.CreateClient();
        var requestStartedAt = Stopwatch.GetTimestamp();
        var requestTask = client.PostAuthAsync(
            "/transaction",
            BuildCreateRequest(context, 25m),
            context.Token);
        await heldLock.WaitForWaitersAsync(1);

        await Task.Delay(TimeSpan.FromMilliseconds(250));
        await heldLock.ReleaseAsync();
        var response = await requestTask;

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var requestElapsed = Stopwatch.GetElapsedTime(requestStartedAt);
        requestElapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(200));
        requestElapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        var measuredWaitMilliseconds = timingSink.GetMeasurement(
            "DailyCloseAccountCoordinationAcquired",
            "WaitDurationMilliseconds",
            context.BranchId,
            context.AccountId);
        measuredWaitMilliseconds.ShouldBeGreaterThan(200d);
        measuredWaitMilliseconds.ShouldBeLessThan(5_000d);
        var measuredHoldMilliseconds = timingSink.GetMeasurement(
            "DailyCloseAccountCoordinationReleased",
            "HoldDurationMilliseconds",
            context.BranchId,
            context.AccountId);
        measuredHoldMilliseconds.ShouldBeGreaterThanOrEqualTo(0d);
        measuredHoldMilliseconds.ShouldBeLessThan(5_000d);
        output.WriteLine(
            "Coordination timing probe: request={0:F2} ms, wait={1:F2} ms, hold={2:F2} ms.",
            requestElapsed.TotalMilliseconds,
            measuredWaitMilliseconds,
            measuredHoldMilliseconds);
    }

    [Fact]
    public async Task Coordination_ShouldHonorClientCancellationWithoutPartialState_ThenRetryCleanly()
    {
        var context = await SeedRaceContextAsync("DcCoordCancellation", DailyCloseStatus.Draft);
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transaction")
        {
            Content = JsonContent.Create(BuildCreateRequest(context, 25m))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.Token);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"test-{Guid.NewGuid():N}");
        using var cancellation = new CancellationTokenSource();
        var blockedTask = client.SendAsync(request, cancellation.Token);
        await heldLock.WaitForWaitersAsync(1);

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await blockedTask);
        (await CountTransactionsAsync(context)).ShouldBe(0);

        await heldLock.ReleaseAsync();
        var retry = await client.PostAuthAsync(
            "/transaction",
            BuildCreateRequest(context, 25m),
            context.Token);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await CountTransactionsAsync(context)).ShouldBe(1);
    }

    [Fact]
    public async Task Coordination_ShouldUseCorrectedPriorCount_WhenPriorSaveWinsLaterSubmitRace()
    {
        var context = await SeedChainRaceContextAsync("DcCoordPriorSubmit");
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var priorClient = grantOrderFactory.CreateClient();
        using var submitClient = grantOrderFactory.CreateClient();
        var priorTask = priorClient.PutAuthAsync(
            $"/dailyclose/{context.PredecessorId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = context.PredecessorVersion,
                Items =
                [
                    new RequestUpsertDailyCloseItemJson
                    {
                        ProductId = context.ProductId,
                        Value = 120m
                    }
                ]
            },
            context.Token);
        await heldLock.WaitForWaitersAsync(1);
        var submitTask = submitClient.PostAuthAsync(
            $"/dailyclose/{context.SuccessorId}/submit",
            context.Token);

        await heldLock.WaitForWaitersAsync(2);
        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        (await priorTask).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await submitTask).StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(context.SuccessorId);
        items.Single(item => item.ProductId == context.CashVarianceProductId).Value.ShouldBe(-40m);
    }

    [Fact]
    public async Task Coordination_ShouldMakeRejectObserveOpeningRecheckDemotion_WhenPredecessorEditWins()
    {
        var context = await SeedChainRaceContextAsync(
            "DcCoordPredecessorReject",
            successorStatus: DailyCloseStatus.Submitted);
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var priorClient = grantOrderFactory.CreateClient();
        using var rejectClient = grantOrderFactory.CreateClient();
        var priorTask = priorClient.PutAuthAsync(
            $"/dailyclose/{context.PredecessorId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = context.PredecessorVersion,
                Items =
                [
                    new RequestUpsertDailyCloseItemJson
                    {
                        ProductId = context.ProductId,
                        Value = 120m
                    }
                ]
            },
            context.Token);
        await heldLock.WaitForWaitersAsync(1);
        var rejectTask = rejectClient.PostAuthAsync(
            $"/dailyclose/{context.SuccessorId}/reject",
            new RequestRejectDailyCloseJson { RejectionReason = "should not land" },
            context.Token);

        await heldLock.WaitForWaitersAsync(2);
        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        (await priorTask).StatusCode.ShouldBe(HttpStatusCode.OK);
        var rejectResponse = await rejectTask;
        rejectResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var successor = await factory.ReloadAsync<DailyClose>(context.SuccessorId);
        successor.ShouldNotBeNull();
        successor.Status.ShouldBe(DailyCloseStatus.Draft);
        successor.RejectionReason.ShouldBeNull();
        successor.OpeningRecheckTriggeredByDailyCloseId.ShouldBe(context.PredecessorId);
    }

    [Fact]
    public async Task Coordination_ShouldSerializeFirstCountBeforeLaterSubmit()
    {
        var context = await SeedChainRaceContextAsync(
            "DcCoordFirstCountSubmit",
            predecessorCounted: false);
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var countClient = grantOrderFactory.CreateClient();
        using var submitClient = grantOrderFactory.CreateClient();
        var countTask = countClient.PutAuthAsync(
            $"/dailyclose/{context.PredecessorId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = context.PredecessorVersion,
                Items = []
            },
            context.Token);
        await heldLock.WaitForWaitersAsync(1);
        var submitTask = submitClient.PostAuthAsync(
            $"/dailyclose/{context.SuccessorId}/submit",
            context.Token);

        await heldLock.WaitForWaitersAsync(2);
        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        (await countTask).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await submitTask).StatusCode.ShouldBe(HttpStatusCode.OK);
        var predecessor = await factory.ReloadAsync<DailyClose>(context.PredecessorId);
        predecessor.ShouldNotBeNull();
        predecessor.ItemsFirstRecordedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Coordination_ShouldExposeNewGapActivity_WhenPriorCreateWinsLaterSubmitRace()
    {
        var context = await SeedChainRaceContextAsync(
            "DcCoordGapSubmit",
            predecessorCounted: false);
        await using var heldLock = await HoldCoordinationLockAsync(context.BranchId, context.AccountId);
        using var grantOrderFactory = CreateGrantOrderFactory();
        using var createClient = grantOrderFactory.CreateClient();
        using var submitClient = grantOrderFactory.CreateClient();
        var createTask = createClient.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder()
                .WithDate(context.PredecessorDate)
                .WithValue(25m)
                .WithAccountId(context.AccountId)
                .WithTransactionTypeId(context.TransactionTypeId)
                .Build(),
            context.Token);
        await heldLock.WaitForWaitersAsync(1);
        var submitTask = submitClient.PostAuthAsync(
            $"/dailyclose/{context.SuccessorId}/submit",
            context.Token);

        await heldLock.WaitForWaitersAsync(2);
        await heldLock.ReleaseAndAssertWaitWithinAsync(GrantOrderLockTimeout);
        (await createTask).StatusCode.ShouldBe(HttpStatusCode.Created);
        var submitResponse = await submitTask;
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await submitResponse.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.DAILYCLOSE_PRIOR_DAY_NOT_COUNTED,
            context.PredecessorDate.ToString("dd/MM/yyyy")));
        (await factory.ReloadAsync<DailyClose>(context.SuccessorId))!.Status
            .ShouldBe(DailyCloseStatus.Draft);
    }

    private async Task<RaceContext> SeedRaceContextAsync(string label, DailyCloseStatus status)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var date = LocalToday();
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            status,
            submittedByOperatorId: op.Id,
            submittedAt: status == DailyCloseStatus.Submitted
                ? DateTime.UtcNow.AddMinutes(-5)
                : null);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, 100m);
        if (status == DailyCloseStatus.Submitted)
        {
            await factory.SeedDailyCloseItemAsync(close.Id, cashVarianceProduct.Id, 100m);
        }

        return new RaceContext(
            token,
            branch.Id,
            account.Id,
            transactionType.Id,
            close.Id,
            cashVarianceProduct.Id,
            date);
    }

    private async Task<ChainRaceContext> SeedChainRaceContextAsync(
        string label,
        DailyCloseStatus successorStatus = DailyCloseStatus.Draft,
        bool predecessorCounted = true)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var predecessorDate = LocalToday().AddDays(-1);
        var predecessor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            predecessorDate,
            DailyCloseStatus.Draft,
            submittedByOperatorId: op.Id,
            itemsRecorded: predecessorCounted);
        if (predecessorCounted)
            await factory.SeedDailyCloseItemAsync(predecessor.Id, product.Id, 100m);
        var successor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday(),
            successorStatus,
            submittedByOperatorId: op.Id,
            submittedAt: successorStatus == DailyCloseStatus.Submitted
                ? DateTime.UtcNow.AddMinutes(-2)
                : null);
        await factory.SeedDailyCloseItemAsync(successor.Id, product.Id, 80m);
        if (successorStatus == DailyCloseStatus.Submitted)
            await factory.SeedDailyCloseItemAsync(successor.Id, cashVarianceProduct.Id, -20m);

        return new ChainRaceContext(
            token,
            branch.Id,
            account.Id,
            transactionType.Id,
            product.Id,
            cashVarianceProduct.Id,
            predecessor.Id,
            predecessor.Version,
            predecessorDate,
            successor.Id);
    }

    private static RequestCreateTransactionJson BuildCreateRequest(RaceContext context, decimal value)
    {
        return new RequestCreateTransactionJsonBuilder()
            .WithDate(context.Date)
            .WithValue(value)
            .WithAccountId(context.AccountId)
            .WithTransactionTypeId(context.TransactionTypeId)
            .Build();
    }

    private async Task<int> CountTransactionsAsync(RaceContext context)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.Transactions.CountAsync(transaction =>
            transaction.BranchId == context.BranchId &&
            transaction.AccountId == context.AccountId &&
            transaction.Date == context.Date);
    }

    private WebApplicationFactory<Program> CreateGrantOrderFactory()
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DailyCloseAccountCoordinationOptions>();
            services.AddSingleton(new DailyCloseAccountCoordinationOptions(GrantOrderLockTimeout));
        }));
    }

    private async Task<HeldAdvisoryLock> HoldCoordinationLockAsync(
        Guid branchId,
        Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var key = DailyCloseAccountCoordinationKey.Compute(branchId, accountId);
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@key)",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync();
        return new HeldAdvisoryLock(connection, transaction, key);
    }

    private async Task<HeldDatabaseLock> HoldDailyCloseRowLockAsync(Guid dailyCloseId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM \"DailyCloses\" WHERE \"Id\" = @dailyCloseId FOR UPDATE",
            connection,
            transaction);
        command.Parameters.AddWithValue("dailyCloseId", dailyCloseId);
        (await command.ExecuteScalarAsync()).ShouldBe(1);
        return new HeldDatabaseLock(connection, transaction);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    private sealed class HeldAdvisoryLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long advisoryKey) : IAsyncDisposable
    {
        private bool _released;
        private long? _firstWaiterObservedAt;

        public async Task WaitForWaitersAsync(int expectedCount)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                await using var command = new NpgsqlCommand(
                    """
                    SELECT COUNT(*)
                    FROM pg_locks
                    WHERE locktype = 'advisory'
                      AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                      AND classid = (((@key >> 32) & 4294967295)::oid)
                      AND objid = ((@key & 4294967295)::oid)
                      AND objsubid = 1
                      AND granted = false
                    """,
                    connection,
                    transaction);
                command.Parameters.AddWithValue("key", advisoryKey);
                var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                if (count >= expectedCount)
                {
                    if (expectedCount == 1)
                        _firstWaiterObservedAt ??= Stopwatch.GetTimestamp();
                    return;
                }

                await Task.Delay(25);
            }

            throw new TimeoutException(
                $"Expected {expectedCount} real requests to wait on the daily-close advisory lock.");
        }

        public async Task ReleaseAsync()
        {
            if (_released)
                return;

            await transaction.CommitAsync();
            _released = true;
        }

        public async Task ReleaseAndAssertWaitWithinAsync(TimeSpan lockTimeout)
        {
            _firstWaiterObservedAt.ShouldNotBeNull(
                "the first real request must be observed waiting before the forced grant is released");
            var observedWait = Stopwatch.GetElapsedTime(_firstWaiterObservedAt.Value);
            observedWait.ShouldBeLessThan(lockTimeout);
            await ReleaseAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class HeldDatabaseLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction) : IAsyncDisposable
    {
        private bool _released;

        public async Task ReleaseAsync()
        {
            if (_released)
                return;

            await transaction.CommitAsync();
            _released = true;
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record RaceContext(
        string Token,
        Guid BranchId,
        Guid AccountId,
        Guid TransactionTypeId,
        Guid CloseId,
        Guid CashVarianceProductId,
        DateTime Date);

    private sealed record ChainRaceContext(
        string Token,
        Guid BranchId,
        Guid AccountId,
        Guid TransactionTypeId,
        Guid ProductId,
        Guid CashVarianceProductId,
        Guid PredecessorId,
        uint PredecessorVersion,
        DateTime PredecessorDate,
        Guid SuccessorId);

    private sealed class CoordinationTimingLogSink : ILoggerProvider, ILogger
    {
        private readonly ConcurrentQueue<CoordinationLogEntry> _entries = [];

        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || state is not IEnumerable<KeyValuePair<string, object?>> values)
                return;

            _entries.Enqueue(new CoordinationLogEntry(
                eventId.Name ?? string.Empty,
                values.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }

        public double GetMeasurement(
            string eventName,
            string measurementName,
            Guid branchId,
            Guid accountId)
        {
            var entry = _entries.LastOrDefault(candidate =>
                candidate.EventName == eventName &&
                candidate.Properties.GetValueOrDefault("BranchId") is Guid loggedBranchId &&
                loggedBranchId == branchId &&
                candidate.Properties.GetValueOrDefault("AccountId") is Guid loggedAccountId &&
                loggedAccountId == accountId);
            entry.ShouldNotBeNull($"expected structured {eventName} timing for the real request");
            entry.Properties.TryGetValue(measurementName, out var measurement).ShouldBeTrue();
            return Convert.ToDouble(measurement, System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed record CoordinationLogEntry(
            string EventName,
            IReadOnlyDictionary<string, object?> Properties);
    }
}
