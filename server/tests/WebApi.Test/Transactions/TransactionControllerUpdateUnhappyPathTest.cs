using System.Net;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerUpdateUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_ShouldReturn403_WhenMemberTargetsUnlinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateMemberScope403", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: callerOperator.Id,
            createdByUserId: user.Id,
            date: DateTime.UtcNow.Date);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transaction.Date.AddDays(1))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenMemberNeedsElevatedRole()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateMemberRole403", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: callerOperator.Id,
            createdByUserId: user.Id,
            date: DateTime.UtcNow.Date.AddDays(-1));
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transaction.Date.AddDays(2))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenMemberHasNoLinkedOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateMemberOperator403", Role.Member);
        var recordedByOperator = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: recordedByOperator.Id,
            createdByUserId: user.Id,
            date: DateTime.UtcNow.Date);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transaction.Date.AddDays(1))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenMemberIsLinkedButIsNotRecordedOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateMemberWrongOperator403", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var recordedByOperator = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: recordedByOperator.Id,
            createdByUserId: user.Id,
            date: DateTime.UtcNow.Date);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transaction.Date.AddDays(1))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenLockDateBlocksTransactionDate()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateLock409", Role.Admin);
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

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenTransactionIsCancelled()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateCancelled409", Role.Manager);
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

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_CANNOT_UPDATE_CANCELLED);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenFiadoClientIsCleared()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateFiado409", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var client = await factory.SeedClientAsync(branch.Id, "Fiado Client");
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            requiresTabAccountAndClient: true);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: DateTime.UtcNow.Date,
            clientId: client.Id);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(transaction.Date.AddDays(1))
            .WithClientId(null)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_FIADO_REQUIRES_CLIENT);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenTransactionBelongsToDifferentBranch()
    {
        var (attackerUser, attackerBranch, _, attackerToken) =
            await factory.SeedFullBranchContextAsync("TxnUpdateAttacker404", Role.Manager);
        await factory.SeedOperatorAsync(attackerBranch.Id, userId: attackerUser.Id);

        var (victimUser, victimBranch, _, _) =
            await factory.SeedFullBranchContextAsync("TxnUpdateVictim404", Role.Manager);
        var victimOperator = await factory.SeedOperatorAsync(victimBranch.Id, userId: victimUser.Id);
        var victimAccount = await factory.SeedAccountAsync(victimBranch.Id, AccountType.Terminal);
        var victimCategory = await factory.SeedCategoryAsync(victimBranch.Id, "Victim Update Category", Direction.In);
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

        var httpResponse = await _client.PutAuthAsync($"/transaction/{victimTransaction.Id}", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }
}
