using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerCancelHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Cancel_ShouldReturn200AndPersistStatusAuditAndCancellationFields_WhenManagerCancelsActive()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelHappy",
            Role.Manager,
            status: TransactionStatus.Active);
        var request = new RequestCancelTransactionJsonBuilder()
            .WithCancellationReason("erro de digitação no valor")
            .Build();

        var beforeUpdate = DateTime.UtcNow;
        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);
        var afterUpdate = DateTime.UtcNow;

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionJson>();
        payload.Id.ShouldBe(ctx.Transaction.Id);
        payload.Status.ShouldBe(TransactionStatus.Cancelled);
        payload.CancellationReason.ShouldBe("erro de digitação no valor");
        payload.CancelledByUserId.ShouldBe(ctx.User.Id);
        payload.CancelledAt.ShouldNotBeNull();
        payload.UpdatedByUserId.ShouldBe(ctx.User.Id);
        payload.UpdatedAt.ShouldNotBeNull();
        payload.UpdatedAt.ShouldBe(payload.CancelledAt);

        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Cancelled);
        persisted.CancellationReason.ShouldBe("erro de digitação no valor");
        persisted.CancelledByUserId.ShouldBe(ctx.User.Id);
        persisted.CancelledAt.ShouldNotBeNull();
        persisted.CancelledAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate.AddSeconds(-1));
        persisted.CancelledAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate.AddSeconds(1));
        persisted.UpdatedAt.ShouldNotBeNull();
        persisted.UpdatedAt.ShouldBe(persisted.CancelledAt);
        persisted.UpdatedByUserId.ShouldBe(ctx.User.Id);
    }

    [Fact]
    public async Task Cancel_ShouldReturn200AndCancelDraftRow_WhenMemberCancelsOwnDraftOnSameLocalBusinessDay()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelMemberDraft",
            Role.Member,
            status: TransactionStatus.Draft);
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_ShouldOnlyMutateTargetRow_WhenInstallmentSiblingsExist()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCancelInstallmentSibling", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas Cheque", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);

        var firstDueDate = DateTime.Today.AddDays(30);
        var createRequest = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithDescription("Cheque parcelado")
            .WithValue(300m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate, Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(30), Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(60), Value = 100m }
            ])
            .Build();

        var createResponse = await _client.PostAuthAsync("/transaction/installment", createRequest, token);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        var ordered = created.Installments.OrderBy(installment => installment.DueDate).ToList();
        ordered.Count.ShouldBe(3);
        var firstRow = ordered[0];
        var middleRow = ordered[1];
        var lastRow = ordered[2];

        var firstBefore = await factory.ReloadAsync<Transaction>(firstRow.Id);
        var lastBefore = await factory.ReloadAsync<Transaction>(lastRow.Id);
        firstBefore.ShouldNotBeNull();
        lastBefore.ShouldNotBeNull();
        var siblingOriginId = firstBefore.OriginTransactionId;

        var cancelRequest = new RequestCancelTransactionJsonBuilder()
            .WithCancellationReason("cliente desistiu desta parcela")
            .Build();
        var cancelResponse = await _client.PostAuthAsync($"/transaction/{middleRow.Id}/cancel", cancelRequest, token);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var middleAfter = await factory.ReloadAsync<Transaction>(middleRow.Id);
        middleAfter.ShouldNotBeNull();
        middleAfter.Status.ShouldBe(TransactionStatus.Cancelled);
        middleAfter.OriginTransactionId.ShouldBe(siblingOriginId);

        var firstAfter = await factory.ReloadAsync<Transaction>(firstRow.Id);
        var lastAfter = await factory.ReloadAsync<Transaction>(lastRow.Id);
        firstAfter.ShouldNotBeNull();
        lastAfter.ShouldNotBeNull();

        firstAfter.Status.ShouldBe(TransactionStatus.Active);
        lastAfter.Status.ShouldBe(TransactionStatus.Active);
        firstAfter.CancelledAt.ShouldBeNull();
        firstAfter.CancelledByUserId.ShouldBeNull();
        firstAfter.CancellationReason.ShouldBeNull();
        lastAfter.CancelledAt.ShouldBeNull();
        lastAfter.CancelledByUserId.ShouldBeNull();
        lastAfter.CancellationReason.ShouldBeNull();
        firstAfter.UpdatedAt.ShouldBe(firstBefore.UpdatedAt);
        firstAfter.UpdatedByUserId.ShouldBe(firstBefore.UpdatedByUserId);
        lastAfter.UpdatedAt.ShouldBe(lastBefore.UpdatedAt);
        lastAfter.UpdatedByUserId.ShouldBe(lastBefore.UpdatedByUserId);
    }

    [Fact]
    public async Task Cancel_ShouldExcludeCancelledRowFromActiveSum_WhenSumActiveValueByAccountAndDateIsCalled()
    {
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelActiveSumExclusion",
            Role.Manager,
            status: TransactionStatus.Active,
            value: 250m);

        var request = new RequestCancelTransactionJsonBuilder().Build();
        var httpResponse = await _client.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var transactionsRepository = scope.ServiceProvider.GetRequiredService<server.Domain.Interfaces.ITransactionsRepository>();
        var sum = await transactionsRepository.SumActiveValueByAccountAndDateAsNoTracking(
            ctx.Transaction.BranchId,
            ctx.Transaction.AccountId,
            ctx.Transaction.Date);

        sum.ShouldBe(0m);
    }

    [Fact]
    public async Task Cancel_ShouldReturn200_WhenInjectedBranchClockMapsUtcInstantToTransactionLocalBusinessDay()
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
        var ctx = await SeedTransactionContextAsync(
            "TxnCancelClockAllow",
            Role.Member,
            status: TransactionStatus.Active,
            date: localBusinessDate);

        var request = new RequestCancelTransactionJsonBuilder().Build();
        var httpResponse = await customClient.PostAuthAsync($"/transaction/{ctx.Transaction.Id}/cancel", request, ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persisted = await factory.ReloadAsync<Transaction>(ctx.Transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(TransactionStatus.Cancelled);
    }

    private async Task<CancelContext> SeedTransactionContextAsync(
        string label,
        Role role,
        TransactionStatus status = TransactionStatus.Active,
        DateTime? date = null,
        decimal value = 10m)
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
            value: value,
            status: status);

        return new CancelContext(user, token, transaction);
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

    private sealed record CancelContext(User User, string Token, Transaction Transaction);
}
