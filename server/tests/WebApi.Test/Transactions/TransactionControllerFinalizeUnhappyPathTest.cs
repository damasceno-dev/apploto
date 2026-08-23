using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerFinalizeUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Finalize_ShouldReturn409_WhenTransactionIsAlreadyActive()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnFinalizeActive409",
            Role.Manager,
            status: TransactionStatus.Active);

        var httpResponse = await _client.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token,
            ctx.Transaction.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_CANNOT_FINALIZE_NON_DRAFT);
    }

    [Fact]
    public async Task Finalize_ShouldReturn400_WhenIfMatchIsMissing()
    {
        var ctx = await SeedTransactionContextAsync("TxnFinalizeMissingIfMatch400", Role.Manager);

        var httpResponse = await _client.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED);
    }

    [Fact]
    public async Task Finalize_ShouldReturnExactStaleWriteKey_WhenDraftWasUpdatedAfterLoad()
    {
        var ctx = await SeedTransactionContextAsync("TxnFinalizeStale409", Role.Manager);
        var updateRequest = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("updated by another manager")
            .WithDueDate(ctx.Transaction.Date.AddDays(30))
            .Build();

        var updateResponse = await _client.PutAuthAsync(
            $"/transaction/{ctx.Transaction.Id}",
            updateRequest,
            ctx.Token,
            ctx.Transaction.Version);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());

        var finalizeResponse = await _client.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token,
            ctx.Transaction.Version);

        finalizeResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict, await finalizeResponse.Content.ReadAsStringAsync());
        var payload = await finalizeResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_STALE_WRITE);

        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Draft);
        persisted.Description.ShouldBe(updateRequest.Description);
        persisted.DueDate.ShouldBe(updateRequest.DueDate);
    }

    [Fact]
    public async Task Finalize_ShouldReturn403_WhenMemberHasLinkedOperatorButNoActiveAccounts()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnFinalizeMemberScope403",
            Role.Member,
            linkCallerToAccount: false);

        var httpResponse = await _client.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token,
            ctx.Transaction.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task Finalize_ShouldReturn403_WhenMemberIsLinkedButIsNotRecordingOperator()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnFinalizeWrongOperator403",
            Role.Member,
            recordedByCaller: false);

        var httpResponse = await _client.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token,
            ctx.Transaction.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
    }

    [Fact]
    public async Task Finalize_ShouldReturn403_WhenInjectedBranchClockMapsUtcTodayToDifferentLocalBusinessDay()
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
        var utcDate = DateTime.UtcNow.Date;
        var ctx = await SeedTransactionContextAsync(
            "TxnFinalizeClockReject",
            Role.Member,
            date: utcDate);

        var httpResponse = await customClient.PostAuthAsync(
            $"/transaction/{ctx.Transaction.Id}/finalize",
            ctx.Token,
            ctx.Transaction.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Draft);
    }

    private async Task<FinalizeContext> SeedTransactionContextAsync(
        string label,
        Role role,
        TransactionStatus status = TransactionStatus.Draft,
        bool linkCallerToAccount = true,
        bool recordedByCaller = true,
        DateTime? date = null)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, role);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var recordedByOperator = recordedByCaller
            ? callerOperator
            : await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        if (linkCallerToAccount)
        {
            await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);
        }

        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transactionDate = date ?? new BranchClock().LocalBusinessDate(DateTime.UtcNow);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: recordedByOperator.Id,
            createdByUserId: user.Id,
            date: transactionDate,
            status: status);

        return new FinalizeContext(token, transaction);
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

    private sealed record FinalizeContext(string Token, Transaction Transaction);
}
