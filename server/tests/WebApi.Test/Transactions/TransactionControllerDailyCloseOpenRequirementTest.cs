using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerDailyCloseOpenRequirementTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(AccountType.Terminal, CloseCase.Missing, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Terminal, CloseCase.Draft, HttpStatusCode.Created)]
    [InlineData(AccountType.Terminal, CloseCase.Submitted, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Terminal, CloseCase.Approved, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Terminal, CloseCase.Rejected, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Tab, CloseCase.Missing, HttpStatusCode.Created)]
    [InlineData(AccountType.BankAccount, CloseCase.Missing, HttpStatusCode.Created)]
    public async Task Create_ShouldApplyTerminalOpenMatrix(
        AccountType accountType,
        CloseCase closeCase,
        HttpStatusCode expectedStatus)
    {
        var context = await SeedContextAsync($"TxnOpenCreate{accountType}{closeCase}", accountType, closeCase);

        var response = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder()
                .WithDate(context.Date)
                .WithValue(25m)
                .WithAccountId(context.AccountId)
                .WithTransactionTypeId(context.TransactionTypeId)
                .Build(),
            context.Token);

        response.StatusCode.ShouldBe(expectedStatus);
        (await CountRowsAsync(context)).ShouldBe(expectedStatus == HttpStatusCode.Created ? 1 : 0);
        if (expectedStatus == HttpStatusCode.Conflict)
        {
            var error = await response.ReadContentAsync<TestResponseErrorJson>();
            error.ErrorMessages.ShouldContain(closeCase == CloseCase.Missing
                ? ResourcesErrorMessages.TRANSACTION_REQUIRES_OPEN_DAILY_CLOSE
                : ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        }
    }

    [Theory]
    [InlineData(AccountType.Terminal, CloseCase.Missing, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Terminal, CloseCase.Draft, HttpStatusCode.Created)]
    [InlineData(AccountType.Terminal, CloseCase.Submitted, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Terminal, CloseCase.Approved, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Terminal, CloseCase.Rejected, HttpStatusCode.Conflict)]
    [InlineData(AccountType.Tab, CloseCase.Missing, HttpStatusCode.Created)]
    [InlineData(AccountType.BankAccount, CloseCase.Missing, HttpStatusCode.Created)]
    public async Task CreateInstallment_ShouldApplyTerminalOpenMatrix(
        AccountType accountType,
        CloseCase closeCase,
        HttpStatusCode expectedStatus)
    {
        var context = await SeedContextAsync(
            $"TxnOpenInstallment{accountType}{closeCase}",
            accountType,
            closeCase,
            SettlementRule.OperatorEnteredCheque);

        var response = await _client.PostAuthAsync(
            "/transaction/installment",
            new RequestCreateTransactionInstallmentJsonBuilder()
                .WithDate(context.Date)
                .WithValue(300m)
                .WithAccountId(context.AccountId)
                .WithTransactionTypeId(context.TransactionTypeId)
                .Build(),
            context.Token);

        response.StatusCode.ShouldBe(expectedStatus);
        (await CountRowsAsync(context)).ShouldBe(expectedStatus == HttpStatusCode.Created ? 3 : 0);
        if (expectedStatus == HttpStatusCode.Conflict)
        {
            var error = await response.ReadContentAsync<TestResponseErrorJson>();
            error.ErrorMessages.ShouldContain(closeCase == CloseCase.Missing
                ? ResourcesErrorMessages.TRANSACTION_REQUIRES_OPEN_DAILY_CLOSE
                : ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        }
    }

    [Fact]
    public async Task Create_ShouldSucceedOnRetryAfterTerminalCloseIsOpened()
    {
        var context = await SeedContextAsync(
            "TxnOpenRetry",
            AccountType.Terminal,
            CloseCase.Missing);
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(context.Date)
            .WithValue(25m)
            .WithAccountId(context.AccountId)
            .WithTransactionTypeId(context.TransactionTypeId)
            .Build();

        var first = await _client.PostAuthAsync("/transaction", request, context.Token);
        first.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CountRowsAsync(context)).ShouldBe(0);

        var open = await _client.PostAuthAsync(
            "/dailyclose",
            new RequestOpenDailyCloseJson { AccountId = context.AccountId, Date = context.Date },
            context.Token);
        open.StatusCode.ShouldBe(HttpStatusCode.Created);

        var retry = await _client.PostAuthAsync("/transaction", request, context.Token);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await CountRowsAsync(context)).ShouldBe(1);
    }

    private async Task<OpenContext> SeedContextAsync(
        string label,
        AccountType accountType,
        CloseCase closeCase,
        SettlementRule settlementRule = SettlementRule.SameDay)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, accountType);
        var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: settlementRule);
        var date = LocalToday();
        if (closeCase != CloseCase.Missing)
        {
            await factory.SeedDailyCloseAsync(
                branch.Id,
                account.Id,
                date,
                (DailyCloseStatus)closeCase,
                submittedByOperatorId: op.Id);
        }

        return new OpenContext(token, branch.Id, account.Id, transactionType.Id, date);
    }

    private async Task<int> CountRowsAsync(OpenContext context)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionsRepository>();
        return await repository.CountByBranchIdAsNoTracking(
            context.BranchId,
            new TransactionListFilter
            {
                AccountId = context.AccountId,
                DateFrom = context.Date,
                DateTo = context.Date,
                Page = 1,
                PageSize = 200
            });
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    public enum CloseCase
    {
        Missing = -1,
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Rejected = 3
    }

    private sealed record OpenContext(
        string Token,
        Guid BranchId,
        Guid AccountId,
        Guid TransactionTypeId,
        DateTime Date);
}
