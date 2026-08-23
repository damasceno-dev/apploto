using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerFinalizeHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Finalize_ShouldReturn200AndPersistStatusAndAudit_WhenManagerFinalizesDraft()
    {
        var ctx = await SeedDraftTransactionContextAsync("TxnFinalizeHappy", Role.Manager);

        var beforeUpdate = DateTime.UtcNow;
        var httpResponse = await _client.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token,
            ctx.Transaction.Version);
        var afterUpdate = DateTime.UtcNow;

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionJson>();
        payload.Id.ShouldBe(ctx.Transaction.Id);
        payload.Status.ShouldBe(TransactionStatus.Active);
        payload.UpdatedAt.ShouldNotBeNull();
        payload.UpdatedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate.AddSeconds(-1));
        payload.UpdatedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate.AddSeconds(1));
        payload.UpdatedByUserId.ShouldBe(ctx.User.Id);
        httpResponse.Headers.ETag.ShouldNotBeNull();
        httpResponse.Headers.ETag.Tag.ShouldBe($"\"{payload.Version}\"");

        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Active);
        persisted.UpdatedAt.ShouldNotBeNull();
        persisted.UpdatedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate.AddSeconds(-1));
        persisted.UpdatedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate.AddSeconds(1));
        persisted.UpdatedByUserId.ShouldBe(ctx.User.Id);
    }

    [Fact]
    public async Task Finalize_ShouldReturn200_WhenInjectedBranchClockMapsUtcInstantToTransactionLocalBusinessDay()
    {
        var branchClock = new PreviousUtcDayBranchClock();
        await using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBranchClock>();
                services.AddSingleton<IBranchClock>(branchClock);
            });
        });
        var customClient = customFactory.CreateClient();
        var localBusinessDate = branchClock.LocalBusinessDate(DateTime.UtcNow);
        var ctx = await SeedDraftTransactionContextAsync(
            "TxnFinalizeClockAllow",
            Role.Member,
            date: localBusinessDate);

        var httpResponse = await customClient.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token,
            ctx.Transaction.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Active);
    }

    private async Task<FinalizeContext> SeedDraftTransactionContextAsync(
        string label,
        Role role,
        DateTime? date = null)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, role);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);

        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transactionDate = date ?? new BranchClock().LocalBusinessDate(DateTime.UtcNow);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: callerOperator.Id,
            createdByUserId: user.Id,
            date: transactionDate,
            status: TransactionStatus.Draft);

        return new FinalizeContext(user, token, transaction);
    }

    private sealed class PreviousUtcDayBranchClock : IBranchClock
    {
        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }

        public DateTime LocalBusinessDateTime(DateTime utcInstant)
        {
            return utcInstant.Date.AddDays(-1);
        }

        public DateTime LocalBusinessDate(DateTime utcInstant)
        {
            return utcInstant.Date.AddDays(-1);
        }

        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
        {
            return localBusinessDate.Date == LocalBusinessDate(utcInstant);
        }
    }

    private sealed record FinalizeContext(User User, string Token, Transaction Transaction);
}
