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
public class TransactionControllerCancelUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Cancel_ShouldReturn400_WhenCancellationReasonIsEmpty()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelEmptyReason400",
            Role.Manager,
            status: TransactionStatus.Active);
        var request = new RequestCancelTransactionJsonBuilder()
            .WithCancellationReason(string.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_CANCELLATION_REASON_EMPTY);
    }

    [Fact]
    public async Task Cancel_ShouldReturn404_WhenTransactionDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("TxnCancelMissing404", Role.Manager);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{Guid.NewGuid()}/cancel", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task Cancel_ShouldReturn404_WhenTransactionBelongsToAnotherBranch()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelCrossBranch404",
            Role.Manager,
            status: TransactionStatus.Active);
        var (_, _, _, otherToken) = await factory.SeedFullBranchContextAsync("TxnCancelOtherBranch", Role.Manager);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, otherToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task Cancel_ShouldReturn409_WhenTransactionIsAlreadyCancelled()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelAlreadyCancelled409",
            Role.Manager,
            status: TransactionStatus.Cancelled);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_ALREADY_CANCELLED);
    }

    [Fact]
    public async Task Cancel_ShouldReturn409_WhenTransactionDateIsAtOrBeforeBranchLockDate()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelLocked409",
            Role.Manager,
            status: TransactionStatus.Active);
        await factory.SeedSettingAsync(ctx.Transaction.BranchId, lockDate: ctx.Transaction.Date);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);

        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Active);
    }

    [Fact]
    public async Task Cancel_ShouldReturn403_WhenMemberHasLinkedOperatorButNoActiveAccounts()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelMemberScope403",
            Role.Member,
            status: TransactionStatus.Active,
            linkCallerToAccount: false);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task Cancel_ShouldReturn403_WhenMemberIsLinkedButIsNotRecordingOperator()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelWrongOperator403",
            Role.Member,
            status: TransactionStatus.Active,
            recordedByCaller: false);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
    }

    [Fact]
    public async Task Cancel_ShouldReturn403_WhenInjectedBranchClockMapsUtcTodayToDifferentLocalBusinessDay()
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
            "TxnCancelClockReject",
            Role.Member,
            status: TransactionStatus.Active,
            date: utcDate);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await customClient.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);

        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Active);
    }

    private async Task<CancelContext> SeedTransactionContextAsync(
        string label,
        Role role,
        TransactionStatus status,
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

        return new CancelContext(token, transaction);
    }

    private sealed class PreviousUtcDayBranchClock : IBranchClock
    {
        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
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

    private sealed record CancelContext(string Token, Transaction Transaction);
}
