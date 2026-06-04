using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerEditPreviewUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EditPreview_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(DateTime.UtcNow.Date.AddDays(1))
            .Build();

        var httpResponse = await _client.PostAsync(
            $"/transaction/{Guid.NewGuid()}/edit-preview",
            JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task EditPreview_ShouldReturn403_WhenCallerIsMember()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreview403", Role.Member);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: DateTime.UtcNow.Date);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transaction.Date.AddDays(1))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{transaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditPreview_ShouldReturn400_WhenDueDateIsBeforeTransactionDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreview400", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transactionDate = DateTime.UtcNow.Date;
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: transactionDate);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transactionDate.AddDays(-1))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{transaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
    }

    [Fact]
    public async Task EditPreview_ShouldReturn404_WhenTransactionBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) =
            await factory.SeedFullBranchContextAsync("TxnEditPreviewAttacker404", Role.Manager);

        var (victimUser, victimBranch, _, _) =
            await factory.SeedFullBranchContextAsync("TxnEditPreviewVictim404", Role.Manager);
        var victimOperator = await factory.SeedOperatorAsync(victimBranch.Id, userId: victimUser.Id);
        var victimAccount = await factory.SeedAccountAsync(victimBranch.Id, AccountType.Terminal);
        var victimCategory = await factory.SeedCategoryAsync(victimBranch.Id, "Victim EditPreview", Direction.In);
        var victimTransactionType = await factory.SeedTransactionTypeAsync(victimCategory.Id);
        var victimTransaction = await factory.SeedTransactionAsync(
            branchId: victimBranch.Id,
            accountId: victimAccount.Id,
            transactionTypeId: victimTransactionType.Id,
            categoryId: victimCategory.Id,
            direction: victimCategory.DefaultDirection,
            recordedByOperatorId: victimOperator.Id,
            createdByUserId: victimUser.Id,
            date: DateTime.UtcNow.Date);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(victimTransaction.Date.AddDays(1))
            .Build();

        var httpResponse = await _client.PostAuthAsync(
            $"/transaction/{victimTransaction.Id}/edit-preview", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task EditPreview_ShouldReturn409_WhenLockDateBlocksTransactionDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewLock409", Role.Admin);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transactionDate = DateTime.UtcNow.Date.AddDays(-1);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: transactionDate);
        await factory.SeedSettingAsync(branch.Id, lockDate: transactionDate);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transactionDate.AddDays(1))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{transaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
    }

    [Fact]
    public async Task EditPreview_ShouldReturn409_WhenTransactionIsCancelled()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnEditPreviewCancelled409", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: DateTime.UtcNow.Date,
            status: TransactionStatus.Cancelled);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transaction.Date.AddDays(1))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/transaction/{transaction.Id}/edit-preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_CANNOT_UPDATE_CANCELLED);
    }
}
