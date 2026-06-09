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
public class TransactionControllerCreatePreviewUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreatePreview_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/transaction/preview", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn400_WhenValueIsZero()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreview400", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithValue(0m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_MUST_BE_POSITIVE);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn403_WhenMemberTargetsUnlinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewMember403", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn400_WhenMemberHasNoLinkedOperator()
    {
        // Preview/write parity: the create flow's RecordedByOperator resolver runs before the
        // account-scope guard, so a Member with no linked operator surfaces an OnValidationException
        // (400), not a 403 — exactly what POST /transaction does for the same caller.
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewNoLink", Role.Member);
        await factory.SeedOperatorAsync(branch.Id); // operator exists but is NOT linked to the caller's user
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn404_WhenTransactionTypeBelongsToDifferentBranch()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewTypeXBranch", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id, "Entradas", Direction.In);
        var otherTransactionType = await factory.SeedTransactionTypeAsync(otherCategory.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(otherTransactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewAcctXBranch", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(otherAccount.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn404_WhenClientBelongsToDifferentBranch()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewClientXBranch", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var otherClient = await factory.SeedClientAsync(otherBranch.Id, "Cross-branch Client");

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithClientId(otherClient.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task CreatePreview_ShouldReturn409_WhenFiadoInvariantViolated()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreatePreviewFiado409", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.SameDay,
            requiresTabAccountAndClient: true);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/preview", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT);
    }
}
