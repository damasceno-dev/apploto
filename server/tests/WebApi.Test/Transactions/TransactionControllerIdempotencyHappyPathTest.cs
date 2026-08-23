using System.Net;
using System.Text.Json;
using CommonTestUtilities.Requests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
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
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public sealed class TransactionControllerIdempotencyHappyPathTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Create_ShouldReplaySameEnvelopeAndPersistOneRow_WhenKeyAndPayloadRepeat()
    {
        var ctx = await SeedSingleContext("TxnIdempotencyReplay");
        var key = $"single-replay-{Guid.NewGuid():N}";
        using var client = factory.CreateClient();

        var first = await client.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);
        var second = await client.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        second.StatusCode.ShouldBe(HttpStatusCode.Created, await second.Content.ReadAsStringAsync());
        var firstPayload = await first.ReadContentAsync<ResponseCreateTransactionJson>();
        var secondPayload = await second.ReadContentAsync<ResponseCreateTransactionJson>();
        secondPayload.ShouldBeEquivalentTo(firstPayload);
        (await CountTransactions(ctx.BranchId)).ShouldBe(1);
    }

    [Fact]
    public async Task Create_ShouldReplayCommittedResponseWithoutWaitingForAccountCoordination()
    {
        var ctx = await SeedSingleContext("TxnIdempotencyReplayWhileAccountBusy");
        var key = $"single-replay-account-busy-{Guid.NewGuid():N}";
        using var firstClient = factory.CreateClient();
        var firstResponse = await firstClient.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await firstResponse.Content.ReadAsStringAsync());
        var firstPayload = await firstResponse.ReadContentAsync<ResponseCreateTransactionJson>();

        await using var heldAccountLock = await HoldAccountCoordinationLock(
            ctx.BranchId,
            ctx.Request.AccountId);
        await using var shortTimeoutFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DailyCloseAccountCoordinationOptions>();
                services.AddSingleton(new DailyCloseAccountCoordinationOptions(TimeSpan.FromSeconds(1)));
            }));
        using var retryClient = shortTimeoutFactory.CreateClient();

        var retryResponse = await retryClient.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);

        retryResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await retryResponse.Content.ReadAsStringAsync());
        var retryPayload = await retryResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        retryPayload.ShouldBeEquivalentTo(firstPayload);
        (await CountTransactions(ctx.BranchId)).ShouldBe(1);
    }

    [Fact]
    public async Task Create_ShouldPersistExactlyOneRow_WhenSameKeyRequestsAreConcurrent()
    {
        var ctx = await SeedSingleContext("TxnIdempotencyConcurrent");
        var key = $"single-concurrent-{Guid.NewGuid():N}";
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key),
            secondClient.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Created);
        var firstPayload = await responses[0].ReadContentAsync<ResponseCreateTransactionJson>();
        var secondPayload = await responses[1].ReadContentAsync<ResponseCreateTransactionJson>();
        secondPayload.Id.ShouldBe(firstPayload.Id);
        secondPayload.Version.ShouldBe(firstPayload.Version);
        (await CountTransactions(ctx.BranchId)).ShouldBe(1);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReplaySamePlanAndPersistOnePlan_WhenKeyAndPayloadRepeat()
    {
        var ctx = await SeedInstallmentContext("TxnInstallmentIdempotencyReplay");
        var key = $"installment-replay-{Guid.NewGuid():N}";
        using var client = factory.CreateClient();

        var first = await client.PostAuthAsync("/transaction/installment", ctx.Request, ctx.Token, key);
        var second = await client.PostAuthAsync("/transaction/installment", ctx.Request, ctx.Token, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        second.StatusCode.ShouldBe(HttpStatusCode.Created, await second.Content.ReadAsStringAsync());
        var firstPayload = await first.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        var secondPayload = await second.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        secondPayload.ShouldBeEquivalentTo(firstPayload);
        (await CountTransactions(ctx.BranchId)).ShouldBe(ctx.Request.Installments.Count);
    }

    [Fact]
    public async Task CreateInstallment_ShouldPersistExactlyOnePlan_WhenSameKeyRequestsAreConcurrent()
    {
        var ctx = await SeedInstallmentContext("TxnInstallmentIdempotencyConcurrent");
        var key = $"installment-concurrent-{Guid.NewGuid():N}";
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAuthAsync("/transaction/installment", ctx.Request, ctx.Token, key),
            secondClient.PostAuthAsync("/transaction/installment", ctx.Request, ctx.Token, key));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Created);
        var firstPayload = await responses[0].ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        var secondPayload = await responses[1].ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        secondPayload.Installments.Select(item => item.Id).ShouldBe(
            firstPayload.Installments.Select(item => item.Id));
        (await CountTransactions(ctx.BranchId)).ShouldBe(ctx.Request.Installments.Count);
    }

    [Fact]
    public async Task Create_ShouldReleaseKeyReservation_WhenBusinessWriteFailsBeforeCommit()
    {
        var label = "TxnIdempotencyRollback";
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.SameDay);
        var date = LocalToday();
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Approved,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-10),
            approvedByUserId: user.Id,
            approvedAt: DateTime.UtcNow.AddMinutes(-5));
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(date)
            .WithValue(4500m)
            .WithDescription("Retry after reopening the close")
            .WithAccountId(account.Id)
            .WithTransactionTypeId(transactionType.Id)
            .Build();
        var key = $"single-rollback-{Guid.NewGuid():N}";
        using var client = factory.CreateClient();

        var frozenResponse = await client.PostAuthAsync("/transaction", request, token, key);

        frozenResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var frozenError = await frozenResponse.ReadContentAsync<TestResponseErrorJson>();
        frozenError.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        (await GetIdempotencyRequests(branch.Id, user.Id, key)).ShouldBeEmpty();

        var reopenResponse = await client.PostAuthAsync($"/dailyclose/{close.Id}/reopen", token);
        reopenResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reopenResponse.Content.ReadAsStringAsync());

        var retryResponse = await client.PostAuthAsync("/transaction", request, token, key);

        retryResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await retryResponse.Content.ReadAsStringAsync());
        var retryPayload = await retryResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        var persisted = await factory.ReloadAsync<Transaction>(retryPayload.Id);
        persisted.ShouldNotBeNull();
        persisted.Value.ShouldBe(request.Value);
        (await CountTransactions(branch.Id)).ShouldBe(1);
        var idempotencyRequest = (await GetIdempotencyRequests(branch.Id, user.Id, key)).ShouldHaveSingleItem();
        idempotencyRequest.ResourceId.ShouldBe(retryPayload.Id);
        idempotencyRequest.ResponseEnvelope.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Create_ShouldReuseExpiredKeyWithFreshEnvelope_WhenRetentionWindowHasElapsed()
    {
        var utcNow = new DateTime(2026, 8, 21, 15, 0, 0, DateTimeKind.Utc);
        await using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBranchClock>();
            services.AddSingleton<IBranchClock>(new FixedBranchClock(utcNow));
        }));
        using var client = customFactory.CreateClient();
        var ctx = await SeedSingleContext("TxnIdempotencyExpiredReuse");
        var key = $"single-expired-{Guid.NewGuid():N}";

        var firstResponse = await client.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await firstResponse.Content.ReadAsStringAsync());
        var firstPayload = await firstResponse.ReadContentAsync<ResponseCreateTransactionJson>();

        await BackdateIdempotencyRequest(
            customFactory.Services,
            ctx.BranchId,
            ctx.UserId,
            key,
            utcNow.AddTicks(-1));
        var reusedRequest = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithValue(4500m)
            .WithDescription(ctx.Request.Description)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .WithAccountId(ctx.Request.AccountId)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .Build();

        var reusedResponse = await client.PostAuthAsync("/transaction", reusedRequest, ctx.Token, key);

        reusedResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await reusedResponse.Content.ReadAsStringAsync());
        var reusedPayload = await reusedResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        reusedPayload.Id.ShouldNotBe(firstPayload.Id);
        var persisted = await factory.ReloadAsync<Transaction>(reusedPayload.Id);
        persisted.ShouldNotBeNull();
        persisted.Value.ShouldBe(4500m);
        (await CountTransactions(ctx.BranchId)).ShouldBe(2);

        var idempotencyRequest = (await GetIdempotencyRequests(ctx.BranchId, ctx.UserId, key)).ShouldHaveSingleItem();
        idempotencyRequest.ResourceId.ShouldBe(reusedPayload.Id);
        idempotencyRequest.CreatedAt.ShouldBe(utcNow);
        idempotencyRequest.ExpiresAt.ShouldBe(utcNow.Add(FinancialCommandIdempotency.Retention));
        var storedEnvelope = JsonSerializer.Deserialize<ResponseCreateTransactionJson>(
            idempotencyRequest.ResponseEnvelope,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        storedEnvelope.ShouldNotBeNull();
        storedEnvelope.Id.ShouldBe(reusedPayload.Id);

        var replayResponse = await client.PostAuthAsync("/transaction", reusedRequest, ctx.Token, key);
        replayResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await replayResponse.Content.ReadAsStringAsync());
        var replayPayload = await replayResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        replayPayload.ShouldBeEquivalentTo(reusedPayload);
        (await CountTransactions(ctx.BranchId)).ShouldBe(2);
    }

    private async Task<SingleContext> SeedSingleContext(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(123.45m)
            .WithAccountId(account.Id)
            .WithTransactionTypeId(transactionType.Id)
            .Build();
        return new SingleContext(branch.Id, user.Id, token, request);
    }

    private async Task<InstallmentContext> SeedInstallmentContext(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Category", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);
        var firstDueDate = DateTime.Today.AddDays(30);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(300m)
            .WithAccountId(account.Id)
            .WithTransactionTypeId(transactionType.Id)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate, Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddMonths(1), Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddMonths(2), Value = 100m }
            ])
            .Build();
        return new InstallmentContext(branch.Id, token, request);
    }

    private async Task<int> CountTransactions(Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.Transactions.CountAsync(transaction => transaction.BranchId == branchId);
    }

    private async Task<List<IdempotencyRequest>> GetIdempotencyRequests(
        Guid branchId,
        Guid userId,
        string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.IdempotencyRequests
            .AsNoTracking()
            .Where(request =>
                request.Endpoint == "POST /transaction" &&
                request.BranchId == branchId &&
                request.UserId == userId &&
                request.Key == key)
            .ToListAsync();
    }

    private static async Task BackdateIdempotencyRequest(
        IServiceProvider services,
        Guid branchId,
        Guid userId,
        string key,
        DateTime expiresAt)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var request = await dbContext.IdempotencyRequests.SingleAsync(row =>
            row.Endpoint == "POST /transaction" &&
            row.BranchId == branchId &&
            row.UserId == userId &&
            row.Key == key);
        request.ExpiresAt = expiresAt;
        await dbContext.SaveChangesAsync();
    }

    private async Task<HeldAccountCoordinationLock> HoldAccountCoordinationLock(
        Guid branchId,
        Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var lockKey = DailyCloseAccountCoordinationKey.Compute(branchId, accountId);
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@key)",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", lockKey);
        await command.ExecuteNonQueryAsync();
        return new HeldAccountCoordinationLock(connection, transaction);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    private sealed class FixedBranchClock(DateTime utcNow) : IBranchClock
    {
        private readonly BranchClock _branchClock = new();

        public DateTime UtcNow() => utcNow;
        public DateTime LocalBusinessDateTime(DateTime utcInstant) =>
            _branchClock.LocalBusinessDateTime(utcInstant);
        public DateTime LocalBusinessDate(DateTime utcInstant) =>
            _branchClock.LocalBusinessDate(utcInstant);
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant) =>
            _branchClock.IsSameLocalDay(localBusinessDate, utcInstant);
    }

    private sealed class HeldAccountCoordinationLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record SingleContext(
        Guid BranchId,
        Guid UserId,
        string Token,
        RequestCreateTransactionJson Request);
    private sealed record InstallmentContext(
        Guid BranchId,
        string Token,
        RequestCreateTransactionInstallmentJson Request);
}
