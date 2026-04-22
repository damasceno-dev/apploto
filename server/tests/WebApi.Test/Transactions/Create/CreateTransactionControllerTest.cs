using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions.Create;

/// <summary>
/// End-to-end coverage for <c>POST /transaction</c> (single create). Validates the
/// real HTTP pipeline including auth filter, validation, branch consistency,
/// member-scope gating, fiado invariant, lock-date guard, and branch isolation.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class CreateTransactionControllerTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldReturn201AndPersistDenormalizedFields_WhenManagerCreatesTransaction()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateMgr", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(new DateTime(2025, 3, 10))
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.BranchId.ShouldBe(branch.Id);
        payload.CategoryId.ShouldBe(category.Id);
        payload.Direction.ShouldBe(Direction.In);
        payload.Status.ShouldBe(TransactionStatus.Active);
        payload.DueDate.ShouldBe(request.Date);
        payload.RecordedByOperatorId.ShouldBe(op.Id);
        payload.CreatedByUserId.ShouldBe(user.Id);

        var persisted = await factory.ReloadAsync<Transaction>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.CategoryId.ShouldBe(category.Id);
        persisted.Direction.ShouldBe(Direction.In);
        persisted.Value.ShouldBe(request.Value);
        persisted.Status.ShouldBe(TransactionStatus.Active);
    }

    [Fact]
    public async Task Create_ShouldReturn201AndSaveAsDraft_WhenSaveAsDraftIsTrue()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateDraft", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithSaveAsDraft(true)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        payload.Status.ShouldBe(TransactionStatus.Draft);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenMemberActsOnLinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateMemberLinked", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateTransactionJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/transaction", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenValueIsZero()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreate400", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithValue(0m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_MUST_BE_POSITIVE);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenMemberTargetsUnlinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateMember403", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        // NOTE: No OperatorAccount link. The member's operator cannot act on this account.
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenMemberOverridesRecordedByOperatorId()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateMemberOverride", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var otherOperator = await factory.SeedOperatorAsync(branch.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithRecordedByOperatorId(otherOperator.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        // Spec M3 §2.5: Member overrides are a shape-level DTO failure (400), not 403.
        // The server always owns the value for Members — any non-null override is malformed.
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_CANNOT_OVERRIDE_RECORDED_BY_OPERATOR);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenTransactionTypeBelongsToDifferentBranch()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateXBranch", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id, "Entradas", Direction.In);
        var otherTransactionType = await factory.SeedTransactionTypeAsync(otherCategory.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(otherTransactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    [Fact]
    public async Task Create_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateAcctXBranch", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(otherAccount.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenFiadoInvariantViolated()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateFiado", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        // Terminal account — not Tab — violates fiado invariant.
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

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT);
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenLockDateBlocksTarget()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateLock", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var targetDate = new DateTime(2025, 3, 10);
        await factory.SeedSettingAsync(branch.Id, lockDate: targetDate); // Locked on this date.

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(targetDate)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
    }

    [Fact]
    public async Task Create_ShouldIsolateByBranch_WhenTokenBranchDiffersFromAccountBranch()
    {
        // Token for branch A.
        var (user, branchA, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateIsolation", Role.Manager);
        await factory.SeedOperatorAsync(branchA.Id, userId: user.Id);

        // Everything else lives in branch B.
        var branchB = await factory.SeedBranchForOtherContextAsync();
        var accountB = await factory.SeedAccountAsync(branchB.Id, AccountType.Terminal);
        var categoryB = await factory.SeedCategoryAsync(branchB.Id, "Entradas", Direction.In);
        var typeB = await factory.SeedTransactionTypeAsync(categoryB.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(typeB.Id)
            .WithAccountId(accountB.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        // Scoped reads return null first — 404 per contract.
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
