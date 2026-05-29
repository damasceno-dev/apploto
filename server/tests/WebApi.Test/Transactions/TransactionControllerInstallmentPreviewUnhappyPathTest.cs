using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerInstallmentPreviewUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PreviewInstallment_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder().Build();

        var httpResponse = await _client.PostAsync(
            "/transaction/installment/preview",
            JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task PreviewInstallment_ShouldReturn400_WhenInstallmentCountIsBelowMinimum()
    {
        var (accountId, transactionTypeId, token) =
            await SeedPreviewInstallmentContextAsync("TxnPreview400Count", SettlementRule.OperatorEnteredCheque);

        var dueDate = DateTime.Today.AddDays(30);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(transactionTypeId)
            .WithAccountId(accountId)
            .WithValue(100m)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson { DueDate = dueDate, Value = 100m }
            ])
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(
            string.Format(
                ResourcesErrorMessages.TRANSACTION_INSTALLMENT_COUNT_OUT_OF_RANGE,
                server.Application.UseCases.Transactions.CreateInstallment.CreateTransactionInstallmentFluentValidation.MinimumInstallments,
                server.Application.UseCases.Transactions.CreateInstallment.CreateTransactionInstallmentFluentValidation.MaximumInstallments));
    }

    [Fact]
    public async Task PreviewInstallment_ShouldReturn400_WhenInstallmentValuesDoNotMatchTotal()
    {
        var (_, _, token) =
            await SeedPreviewInstallmentContextAsync("TxnPreview400Sum", SettlementRule.OperatorEnteredCheque);

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithValue(301m)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_TOTAL_MISMATCH);
    }

    [Fact]
    public async Task PreviewInstallment_ShouldReturn400_WhenFirstDueDateIsInThePast()
    {
        var (accountId, transactionTypeId, token) =
            await SeedPreviewInstallmentContextAsync("TxnPreview400Past", SettlementRule.OperatorEnteredCheque);

        var pastDate = DateTime.Today.AddDays(-1);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(transactionTypeId)
            .WithAccountId(accountId)
            .WithDate(DateTime.Today.AddDays(-5))
            .WithValue(200m)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson { DueDate = pastDate, Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = pastDate.AddDays(30), Value = 100m }
            ])
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_FIRST_DUE_DATE_MUST_BE_FUTURE);
    }

    [Fact]
    public async Task PreviewInstallment_ShouldReturn403_WhenMemberTargetsUnlinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "TxnPreview403Member", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.SameDay);

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task PreviewInstallment_ShouldReturn409_WhenTransactionTypeIsNotCheque()
    {
        var (accountId, transactionTypeId, token) =
            await SeedPreviewInstallmentContextAsync("TxnPreview409NonCheque", SettlementRule.SameDay);

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(transactionTypeId)
            .WithAccountId(accountId)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);
    }

    [Fact]
    public async Task PreviewInstallment_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnPreview404AcctXBranch", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(otherAccount.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task PreviewInstallment_ShouldReturn404_WhenTransactionTypeBelongsToDifferentBranch()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnPreview404TypeXBranch", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id, "Entradas", Direction.In);
        var otherTransactionType = await factory.SeedTransactionTypeAsync(
            otherCategory.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(otherTransactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    private async Task<(Guid AccountId, Guid TransactionTypeId, string Token)> SeedPreviewInstallmentContextAsync(
        string label,
        SettlementRule settlementRule)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: settlementRule);

        return (account.Id, transactionType.Id, token);
    }
}
