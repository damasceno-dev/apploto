using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public sealed class TransactionControllerIdempotencyUnhappyPathTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Create_ShouldReturnExactConflictKey_WhenSameKeyHasDifferentPayload()
    {
        var ctx = await SeedContext("TxnIdempotencyMismatch");
        var key = $"mismatch-{Guid.NewGuid():N}";
        using var client = factory.CreateClient();
        var changed = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithValue(ctx.Request.Value + 1m)
            .WithAccountId(ctx.Request.AccountId)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .Build();

        var first = await client.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);
        var conflict = await client.PostAuthAsync("/transaction", changed, ctx.Token, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict, await conflict.Content.ReadAsStringAsync());
        (await conflict.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.IDEMPOTENCY_KEY_PAYLOAD_CONFLICT);
    }

    [Fact]
    public async Task Create_ShouldReturnExactValidationKey_WhenIdempotencyHeaderIsMissing()
    {
        var ctx = await SeedContext("TxnIdempotencyMissing");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/transaction")
        {
            Content = JsonContent.Create(ctx.Request)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.Token);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.IDEMPOTENCY_KEY_REQUIRED);
    }

    [Fact]
    public async Task Create_ShouldReturnExactCoordinationConflict_WhenIdempotencyKeyIsHeld()
    {
        var ctx = await SeedContext("TxnIdempotencyCoordinationBusy");
        var key = $"held-{Guid.NewGuid():N}";
        await using var heldLock = await HoldIdempotencyLock(
            "POST /transaction",
            ctx.BranchId,
            ctx.UserId,
            key);
        await using var shortTimeoutFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IdempotencyRequestCoordinationOptions>();
                services.AddSingleton(new IdempotencyRequestCoordinationOptions(
                    TimeSpan.FromMilliseconds(100)));
            }));
        using var client = shortTimeoutFactory.CreateClient();

        var response = await client.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.IDEMPOTENCY_COORDINATION_BUSY);
        (await CountTransactions(ctx.BranchId)).ShouldBe(0);
        (await CountIdempotencyRequests(ctx.BranchId, ctx.UserId, key)).ShouldBe(0);

        await heldLock.Release();
        var retry = await client.PostAuthAsync("/transaction", ctx.Request, ctx.Token, key);

        retry.StatusCode.ShouldBe(HttpStatusCode.Created, await retry.Content.ReadAsStringAsync());
        (await CountTransactions(ctx.BranchId)).ShouldBe(1);
        (await CountIdempotencyRequests(ctx.BranchId, ctx.UserId, key)).ShouldBe(1);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturnExactConflictKey_WhenSameKeyHasDifferentPayload()
    {
        var ctx = await SeedInstallmentContext("TxnInstallmentIdempotencyMismatch");
        var key = $"installment-mismatch-{Guid.NewGuid():N}";
        using var client = factory.CreateClient();
        var changed = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithValue(ctx.Request.Value)
            .WithDescription("same valid plan, changed description")
            .WithAccountId(ctx.Request.AccountId)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithInstallments(ctx.Request.Installments)
            .Build();

        var first = await client.PostAuthAsync("/transaction/installment", ctx.Request, ctx.Token, key);
        var conflict = await client.PostAuthAsync("/transaction/installment", changed, ctx.Token, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict, await conflict.Content.ReadAsStringAsync());
        (await conflict.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.IDEMPOTENCY_KEY_PAYLOAD_CONFLICT);
    }

    private async Task<Context> SeedContext(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(50m)
            .WithAccountId(account.Id)
            .WithTransactionTypeId(transactionType.Id)
            .Build();
        return new Context(branch.Id, user.Id, token, request);
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
        return new InstallmentContext(token, request);
    }

    private async Task<HeldIdempotencyLock> HoldIdempotencyLock(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var lockKey = IdempotencyRequestLockKey.Compute(endpoint, branchId, userId, key);
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@key)",
            connection,
            transaction);
        command.Parameters.AddWithValue("key", lockKey);
        await command.ExecuteNonQueryAsync();
        return new HeldIdempotencyLock(connection, transaction);
    }

    private async Task<int> CountTransactions(Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.Transactions.CountAsync(transaction => transaction.BranchId == branchId);
    }

    private async Task<int> CountIdempotencyRequests(Guid branchId, Guid userId, string key)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.IdempotencyRequests.CountAsync(request =>
            request.Endpoint == "POST /transaction" &&
            request.BranchId == branchId &&
            request.UserId == userId &&
            request.Key == key);
    }

    private sealed class HeldIdempotencyLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction) : IAsyncDisposable
    {
        private bool _released;

        public async Task Release()
        {
            if (_released)
                return;

            await transaction.RollbackAsync();
            _released = true;
        }

        public async ValueTask DisposeAsync()
        {
            await Release();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record Context(
        Guid BranchId,
        Guid UserId,
        string Token,
        RequestCreateTransactionJson Request);
    private sealed record InstallmentContext(string Token, RequestCreateTransactionInstallmentJson Request);
}
