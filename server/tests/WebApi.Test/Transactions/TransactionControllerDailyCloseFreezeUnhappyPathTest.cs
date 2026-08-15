using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerDailyCloseFreezeUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(DailyCloseStatus.Submitted, LedgerMutation.Create)]
    [InlineData(DailyCloseStatus.Approved, LedgerMutation.Create)]
    [InlineData(DailyCloseStatus.Rejected, LedgerMutation.Create)]
    [InlineData(DailyCloseStatus.Submitted, LedgerMutation.CreateInstallment)]
    [InlineData(DailyCloseStatus.Approved, LedgerMutation.CreateInstallment)]
    [InlineData(DailyCloseStatus.Rejected, LedgerMutation.CreateInstallment)]
    [InlineData(DailyCloseStatus.Submitted, LedgerMutation.Finalize)]
    [InlineData(DailyCloseStatus.Approved, LedgerMutation.Finalize)]
    [InlineData(DailyCloseStatus.Rejected, LedgerMutation.Finalize)]
    [InlineData(DailyCloseStatus.Submitted, LedgerMutation.Cancel)]
    [InlineData(DailyCloseStatus.Approved, LedgerMutation.Cancel)]
    [InlineData(DailyCloseStatus.Rejected, LedgerMutation.Cancel)]
    public async Task Mutation_ShouldReturn409AndPreserveLedger_WhenDailyCloseIsFrozen(
        DailyCloseStatus closeStatus,
        LedgerMutation mutation)
    {
        var label = $"TxnCloseFreeze{mutation}{closeStatus}";
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            $"{label} Type",
            mutation == LedgerMutation.CreateInstallment
                ? SettlementRule.OperatorEnteredCheque
                : SettlementRule.SameDay);
        var date = LocalToday();
        await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            closeStatus,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-10),
            approvedByUserId: closeStatus == DailyCloseStatus.Approved ? user.Id : null,
            approvedAt: closeStatus == DailyCloseStatus.Approved ? DateTime.UtcNow.AddMinutes(-5) : null);

        Transaction? existingTransaction = null;
        if (mutation is LedgerMutation.Finalize or LedgerMutation.Cancel)
        {
            existingTransaction = await factory.SeedTransactionAsync(
                branch.Id,
                account.Id,
                transactionType.Id,
                category.Id,
                category.DefaultDirection,
                op.Id,
                user.Id,
                date,
                status: mutation == LedgerMutation.Finalize
                    ? TransactionStatus.Draft
                    : TransactionStatus.Active);
        }

        var beforeCount = await CountRowsAsync(branch.Id, account.Id, date);
        var response = await SendMutationAsync(
            mutation,
            token,
            account.Id,
            transactionType.Id,
            date,
            existingTransaction?.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        (await CountRowsAsync(branch.Id, account.Id, date)).ShouldBe(beforeCount);

        if (existingTransaction is not null)
        {
            var persisted = await factory.ReloadAsync<Transaction>(existingTransaction.Id);
            persisted.ShouldNotBeNull();
            persisted.Status.ShouldBe(existingTransaction.Status);
            persisted.CancelledAt.ShouldBeNull();
            persisted.CancelledByUserId.ShouldBeNull();
        }
    }

    private Task<HttpResponseMessage> SendMutationAsync(
        LedgerMutation mutation,
        string token,
        Guid accountId,
        Guid transactionTypeId,
        DateTime date,
        Guid? transactionId)
    {
        return mutation switch
        {
            LedgerMutation.Create => _client.PostAuthAsync(
                "/transaction",
                new RequestCreateTransactionJsonBuilder()
                    .WithDate(date)
                    .WithValue(25m)
                    .WithAccountId(accountId)
                    .WithTransactionTypeId(transactionTypeId)
                    .Build(),
                token),
            LedgerMutation.CreateInstallment => _client.PostAuthAsync(
                "/transaction/installment",
                new RequestCreateTransactionInstallmentJsonBuilder()
                    .WithDate(date)
                    .WithValue(300m)
                    .WithAccountId(accountId)
                    .WithTransactionTypeId(transactionTypeId)
                    .Build(),
                token),
            LedgerMutation.Finalize => _client.PostAuthAsync(
                $"/transaction/{transactionId!.Value}/finalize",
                token),
            LedgerMutation.Cancel => _client.PostAuthAsync(
                $"/transaction/{transactionId!.Value}/cancel",
                new RequestCancelTransactionJsonBuilder().Build(),
                token),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
    }

    private async Task<int> CountRowsAsync(Guid branchId, Guid accountId, DateTime date)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionsRepository>();
        return await repository.CountByBranchIdAsNoTracking(
            branchId,
            new TransactionListFilter
            {
                AccountId = accountId,
                DateFrom = date,
                DateTo = date,
                Page = 1,
                PageSize = 200
            });
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    public enum LedgerMutation
    {
        Create,
        CreateInstallment,
        Finalize,
        Cancel
    }
}
