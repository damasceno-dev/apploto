using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;

namespace WebApi.Test.Settings;

[Collection(ServerApiCollection.Name)]
public sealed class SettingControllerLockMonthCoordinationTest(ServerWebApplicationFactory factory)
{
    private static readonly TimeSpan GrantOrderTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task LockFirst_ShouldLockAndRejectQueuedLedgerWrite_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRaceLedger");
        var bank = await factory.SeedAccountAsync(ctx.BranchId, AccountType.BankAccount);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var lockClient = grantOrderFactory.CreateClient();
        using var ledgerClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(1);
        var ledgerTask = ledgerClient.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder()
                .WithDate(ctx.Date)
                .WithValue(25m)
                .WithAccountId(bank.Id)
                .WithTransactionTypeId(ctx.TransactionTypeId)
                .WithRecordedByOperatorId(ctx.OperatorId)
                .Build(),
            ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(lockTask, ledgerTask).WaitAsync(TimeSpan.FromSeconds(10));
        var lockResponse = await lockTask;
        var ledgerResponse = await ledgerTask;

        lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        ledgerResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ledgerResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await dbContext.Transactions.CountAsync(transaction =>
            transaction.BranchId == ctx.BranchId && transaction.AccountId == bank.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task LockFirst_ShouldLockAndRejectQueuedTimeEntrySegmentWrite_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRaceTimeEntrySegment");
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            ctx.BranchId,
            ctx.OperatorId,
            ctx.Date,
            TimeEntryStatus.Present,
            (ctx.Date.AddHours(8), ctx.Date.AddHours(12), true));
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id))
            .Segments.ShouldHaveSingleItem().Id;
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var lockClient = grantOrderFactory.CreateClient();
        using var segmentClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(1);
        var segmentTask = segmentClient.PutAuthAsync(
            $"/timeentry/segment/{segmentId}",
            new RequestUpdateTimeEntrySegmentJsonBuilder()
                .WithClockIn(ctx.Date.AddHours(9))
                .WithClockOut(ctx.Date.AddHours(13))
                .Build(),
            ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(lockTask, segmentTask).WaitAsync(TimeSpan.FromSeconds(10));
        var lockResponse = await lockTask;
        var segmentResponse = await segmentTask;

        lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        segmentResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await segmentResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
        var persisted = await TimeEntrySegmentTestHelpers.ReloadSegmentAsync(factory, segmentId);
        persisted.ClockIn.ShouldBe(ctx.Date.AddHours(8));
        persisted.ClockOut.ShouldBe(ctx.Date.AddHours(12));
    }

    [Fact]
    public async Task LockFirst_ShouldLockAndRejectQueuedTimeEntrySnapshotWrite_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRaceTimeEntrySnapshot");
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            ctx.BranchId,
            ctx.OperatorId,
            ctx.Date,
            TimeEntryStatus.Present,
            (ctx.Date.AddHours(8), ctx.Date.AddHours(12), true));
        var persistedBefore = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        var segment = persistedBefore.Segments.ShouldHaveSingleItem();
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(ctx.OperatorId)
            .WithDate(ctx.Date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(new RequestTimeEntrySegmentJsonBuilder()
                .WithId(segment.Id)
                .WithClockIn(segment.ClockIn)
                .WithClockOut(ctx.Date.AddHours(13))
                .Build());
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var lockClient = grantOrderFactory.CreateClient();
        using var timeEntryClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(1);
        var upsertTask = timeEntryClient.PutAuthAsync("/timeentry", request, ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(lockTask, upsertTask).WaitAsync(TimeSpan.FromSeconds(10));
        var lockResponse = await lockTask;
        var upsertResponse = await upsertTask;

        lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        upsertResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await upsertResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
        var persistedAfter = await TimeEntrySegmentTestHelpers.ReloadSegmentAsync(factory, segment.Id);
        persistedAfter.ClockOut.ShouldBe(ctx.Date.AddHours(12));
    }

    [Fact]
    public async Task LockFirst_ShouldLockAndRejectQueuedTimeEntryDeactivate_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRaceTimeEntryDeactivate");
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            ctx.BranchId,
            ctx.OperatorId,
            ctx.Date,
            TimeEntryStatus.Present,
            (ctx.Date.AddHours(8), ctx.Date.AddHours(12), true));
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var lockClient = grantOrderFactory.CreateClient();
        using var timeEntryClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(1);
        var deactivateTask = timeEntryClient.DeleteAuthAsync($"/timeentry/{entry.Id}", ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(lockTask, deactivateTask).WaitAsync(TimeSpan.FromSeconds(10));
        var lockResponse = await lockTask;
        var deactivateResponse = await deactivateTask;

        lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await deactivateResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
        (await factory.ReloadAsync<TimeEntry>(entry.Id))!.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task PreFloorDraftWriteFirst_ShouldPersistAndMakeQueuedLockValidateEarlierMonth_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRacePreFloorLedger");
        var bank = await factory.SeedAccountAsync(ctx.BranchId, AccountType.BankAccount);
        var earlierMonth = ctx.Month.AddMonths(-1);
        var earlierDate = earlierMonth.AddDays(8);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var ledgerClient = grantOrderFactory.CreateClient();
        using var lockClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var ledgerTask = ledgerClient.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder()
                .WithDate(earlierDate)
                .WithValue(25m)
                .WithAccountId(bank.Id)
                .WithTransactionTypeId(ctx.TransactionTypeId)
                .WithRecordedByOperatorId(ctx.OperatorId)
                .WithSaveAsDraft(true)
                .Build(),
            ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(ledgerTask, lockTask).WaitAsync(TimeSpan.FromSeconds(10));
        var ledgerResponse = await ledgerTask;
        var lockResponse = await lockTask;

        ledgerResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await ledgerResponse.Content.ReadAsStringAsync());
        lockResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict, await lockResponse.Content.ReadAsStringAsync());
        (await lockResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(IntermediateUnresolvedMessage(earlierMonth));
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(DateTime.MinValue);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await dbContext.Transactions.CountAsync(transaction =>
            transaction.BranchId == ctx.BranchId &&
            transaction.AccountId == bank.Id &&
            transaction.Date == earlierDate &&
            transaction.Status == TransactionStatus.Draft)).ShouldBe(1);
    }

    [Fact]
    public async Task LockFirst_ShouldLockAndRejectQueuedReopen_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRaceReopen");
        var close = await SeedClose(ctx, DailyCloseStatus.Approved);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var lockClient = grantOrderFactory.CreateClient();
        using var closeClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(1);
        var reopenTask = closeClient.PostAuthAsync($"/dailyclose/{close.Id}/reopen", ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(lockTask, reopenTask).WaitAsync(TimeSpan.FromSeconds(10));
        var lockResponse = await lockTask;
        var reopenResponse = await reopenTask;

        lockResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await lockResponse.Content.ReadAsStringAsync());
        reopenResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await reopenResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        (await factory.ReloadAsync<DailyClose>(close.Id))!.Status.ShouldBe(DailyCloseStatus.Approved);
    }

    [Fact]
    public async Task FirstCountFirst_ShouldPersistCountAndMakeQueuedLockFail_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRaceFirstCount");
        var close = await factory.SeedDailyCloseAsync(
            ctx.BranchId,
            ctx.TerminalId,
            ctx.Date,
            DailyCloseStatus.Draft,
            itemsRecorded: false);
        var version = await GetVersion(ctx, close.Id);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var countClient = grantOrderFactory.CreateClient();
        using var lockClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var countTask = countClient.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = ctx.ProductId, Value = 10m }]
            },
            ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(countTask, lockTask).WaitAsync(TimeSpan.FromSeconds(10));
        var countResponse = await countTask;
        var lockResponse = await lockTask;

        countResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await countResponse.Content.ReadAsStringAsync());
        lockResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await lockResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SETTING_LOCK_MONTH_UNAPPROVED_CLOSE);
        (await factory.ReloadAsync<DailyClose>(close.Id))!.ItemsFirstRecordedAt.ShouldNotBeNull();
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public async Task RecallFirst_ShouldPersistRecallAndMakeQueuedLockFail_WithoutDeadlock()
    {
        var ctx = await SeedContext("SettingLockRaceRecall");
        var close = await SeedClose(ctx, DailyCloseStatus.Submitted);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var recallClient = grantOrderFactory.CreateClient();
        using var lockClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var recallTask = recallClient.PostAuthAsync($"/dailyclose/{close.Id}/recall", ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(recallTask, lockTask).WaitAsync(TimeSpan.FromSeconds(10));
        var recallResponse = await recallTask;
        var lockResponse = await lockTask;

        recallResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await recallResponse.Content.ReadAsStringAsync());
        lockResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await lockResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SETTING_LOCK_MONTH_UNAPPROVED_CLOSE);
        (await factory.ReloadAsync<DailyClose>(close.Id))!.Status.ShouldBe(DailyCloseStatus.Draft);
    }

    [Fact]
    public async Task PriorDayCorrectionFirst_ShouldPersistCorrectionAndMakeQueuedLockFail()
    {
        var ctx = await SeedContext("SettingLockRaceCorrection");
        var close = await SeedClose(ctx, DailyCloseStatus.Rejected);
        await factory.SeedDailyCloseItemAsync(close.Id, ctx.ProductId, 10m);
        var version = await GetVersion(ctx, close.Id);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var correctionClient = grantOrderFactory.CreateClient();
        using var lockClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var correctionTask = correctionClient.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = ctx.ProductId, Value = 20m }]
            },
            ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var lockTask = Lock(lockClient, ctx);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(correctionTask, lockTask).WaitAsync(TimeSpan.FromSeconds(10));
        var correctionResponse = await correctionTask;
        var lockResponse = await lockTask;

        correctionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await correctionResponse.Content.ReadAsStringAsync());
        lockResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await lockResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SETTING_LOCK_MONTH_UNAPPROVED_CLOSE);
        (await factory.ReloadAsync<DailyClose>(close.Id))!.Status.ShouldBe(DailyCloseStatus.Draft);
    }

    [Fact]
    public async Task LockMonth_ShouldRejectPhase3CascadeDemotedDraft()
    {
        var ctx = await SeedContext("SettingLockCascadeDraft");
        var predecessor = await SeedClose(ctx, DailyCloseStatus.Approved, ctx.Date);
        var successor = await SeedClose(ctx, DailyCloseStatus.Approved, ctx.Date.AddDays(1));
        await factory.SeedDailyCloseItemAsync(predecessor.Id, ctx.ProductId, 10m);
        await factory.SeedDailyCloseItemAsync(successor.Id, ctx.ProductId, 30m);
        using var client = factory.CreateClient();

        var reopen = await client.PostAuthAsync($"/dailyclose/{predecessor.Id}/reopen", ctx.Token);
        reopen.StatusCode.ShouldBe(HttpStatusCode.OK, await reopen.Content.ReadAsStringAsync());
        var reopened = await reopen.ReadContentAsync<ResponseDailyCloseJson>();
        var correction = await client.PutAuthAsync(
            $"/dailyclose/{predecessor.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = reopened.Version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = ctx.ProductId, Value = 20m }]
            },
            ctx.Token);
        correction.StatusCode.ShouldBe(HttpStatusCode.OK, await correction.Content.ReadAsStringAsync());
        var correctionPayload = await correction.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        correctionPayload.AffectedSuccessor.ShouldNotBeNull();
        correctionPayload.AffectedSuccessor.DailyCloseId.ShouldBe(successor.Id);

        var submit = await client.PostAuthAsync($"/dailyclose/{predecessor.Id}/submit", ctx.Token);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var approve = await client.PostAuthAsync($"/dailyclose/{predecessor.Id}/approve", ctx.Token);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());

        var lockResponse = await Lock(client, ctx);

        lockResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await lockResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SETTING_LOCK_MONTH_UNAPPROVED_CLOSE);
        (await factory.ReloadAsync<DailyClose>(successor.Id))!.Status.ShouldBe(DailyCloseStatus.Draft);
    }

    [Fact]
    public async Task TwoConcurrentLockMonthCommands_ShouldSerializeToSuccessThenRejectStaleRoot()
    {
        var ctx = await SeedContext("SettingLockRaceTwoCommands");
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var firstClient = grantOrderFactory.CreateClient();
        using var secondClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var firstTask = Lock(firstClient, ctx);
        await heldLock.WaitForWaitersAsync(1);
        var secondTask = Lock(secondClient, ctx);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(10));
        var firstResponse = await firstTask;
        var secondResponse = await secondTask;

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict, await secondResponse.Content.ReadAsStringAsync());
        (await secondResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SETTING_STALE_WRITE);
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate
            .ShouldBe(ctx.Month.AddMonths(1).AddDays(-1));
    }

    [Fact]
    public async Task LockMonth_ShouldReturnExactCoordinationBusyKey_WhenPostgresBoundaryTimesOut()
    {
        var ctx = await SeedContext("SettingLockPostgresBusy");
        await using var busyFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<MonthLockCoordinationOptions>();
            services.AddSingleton(new MonthLockCoordinationOptions(TimeSpan.FromMilliseconds(100)));
        }));
        using var client = busyFactory.CreateClient();
        await using var heldLock = await HoldMonthLockAsync(ctx.BranchId);

        var response = await Lock(client, ctx);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SETTING_LOCK_MONTH_COORDINATION_BUSY);
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(DateTime.MinValue);
    }

    private WebApplicationFactory<Program> CreateGrantOrderFactory()
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DailyCloseAccountCoordinationOptions>();
            services.AddSingleton(new DailyCloseAccountCoordinationOptions(GrantOrderTimeout));
            services.RemoveAll<MonthLockCoordinationOptions>();
            services.AddSingleton(new MonthLockCoordinationOptions(GrantOrderTimeout));
        }));
    }

    private async Task<RaceContext> SeedContext(string label)
    {
        var month = CurrentMonth().AddMonths(-1);
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            label,
            Role.Admin,
            month.AddDays(1).AddHours(12));
        var setting = await factory.SeedSettingAsync(branch.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 1);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 2);

        return new RaceContext(
            user.Id,
            branch.Id,
            setting.Id,
            setting.Version,
            terminal.Id,
            op.Id,
            category.Id,
            transactionType.Id,
            product.Id,
            token,
            month,
            month.AddDays(8));
    }

    private Task<DailyClose> SeedClose(
        RaceContext ctx,
        DailyCloseStatus status,
        DateTime? date = null)
    {
        var isSubmittedOrLater = status is DailyCloseStatus.Submitted or DailyCloseStatus.Approved;
        return factory.SeedDailyCloseAsync(
            ctx.BranchId,
            ctx.TerminalId,
            date ?? ctx.Date,
            status,
            submittedByOperatorId: isSubmittedOrLater ? ctx.OperatorId : null,
            submittedAt: isSubmittedOrLater ? DateTime.UtcNow : null,
            approvedByUserId: status == DailyCloseStatus.Approved ? ctx.UserId : null,
            approvedAt: status == DailyCloseStatus.Approved ? DateTime.UtcNow : null,
            rejectionReason: status == DailyCloseStatus.Rejected ? "Corrigir contagem" : null,
            recordedByOperatorId: ctx.OperatorId,
            recordedByUserId: ctx.UserId,
            submittedByUserId: isSubmittedOrLater ? ctx.UserId : null);
    }

    private static Task<HttpResponseMessage> Lock(HttpClient client, RaceContext ctx)
    {
        return client.PostAuthAsync(
            "/setting/lock-month",
            new RequestLockSettingMonthJson { Year = ctx.Month.Year, Month = ctx.Month.Month },
            ctx.Token,
            expectedVersion: ctx.SettingVersion);
    }

    private async Task<uint> GetVersion(RaceContext ctx, Guid closeId)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAuthAsync($"/dailyclose/{closeId}", ctx.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponseDailyCloseJson>()).Version;
    }

    private async Task<HeldMonthLock> HoldMonthLockAsync(Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var key = MonthLockCoordinationKey.Compute(branchId);
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@key)",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync();
        return new HeldMonthLock(connection, transaction, key);
    }

    private static DateTime CurrentMonth()
    {
        var today = new BranchClock().LocalBusinessDate(DateTime.UtcNow);
        return new DateTime(today.Year, today.Month, 1);
    }

    private static string IntermediateUnresolvedMessage(DateTime month) => string.Format(
        ResourcesErrorMessages.SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED,
        month.ToString("MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));

    private sealed class HeldMonthLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long advisoryKey) : IAsyncDisposable
    {
        private bool _released;

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
                    return;

                await Task.Delay(25);
            }

            throw new TimeoutException($"Expected {expectedCount} requests to wait on the month-lock key.");
        }

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
        Guid UserId,
        Guid BranchId,
        Guid SettingId,
        uint SettingVersion,
        Guid TerminalId,
        Guid OperatorId,
        Guid CategoryId,
        Guid TransactionTypeId,
        Guid ProductId,
        string Token,
        DateTime Month,
        DateTime Date);
}
