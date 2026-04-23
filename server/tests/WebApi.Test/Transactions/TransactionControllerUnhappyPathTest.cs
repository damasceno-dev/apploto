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
public class TransactionControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

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

        var targetDate = DateTime.Today;
        await factory.SeedSettingAsync(branch.Id, lockDate: targetDate);

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
        var (user, branchA, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateIsolation", Role.Manager);
        await factory.SeedOperatorAsync(branchA.Id, userId: user.Id);

        var branchB = await factory.SeedBranchForOtherContextAsync();
        var accountB = await factory.SeedAccountAsync(branchB.Id, AccountType.Terminal);
        var categoryB = await factory.SeedCategoryAsync(branchB.Id, "Entradas", Direction.In);
        var typeB = await factory.SeedTransactionTypeAsync(categoryB.Id);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(typeB.Id)
            .WithAccountId(accountB.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenTransactionBelongsToDifferentBranch()
    {
        var (attackerUser, attackerBranch, _, attackerToken) =
            await factory.SeedFullBranchContextAsync("TxnGetAttacker", Role.Manager);
        await factory.SeedOperatorAsync(attackerBranch.Id, userId: attackerUser.Id);

        var (victimUser, victimBranch, _, _) =
            await factory.SeedFullBranchContextAsync("TxnGetVictim", Role.Manager);
        var victimOperator = await factory.SeedOperatorAsync(victimBranch.Id, userId: victimUser.Id);
        var victimAccount = await factory.SeedAccountAsync(victimBranch.Id, AccountType.Terminal);
        var victimCategory = await factory.SeedCategoryAsync(victimBranch.Id, "Victim Category", Direction.In);
        var victimTransactionType = await factory.SeedTransactionTypeAsync(victimCategory.Id);
        var victimTransaction = await factory.SeedTransactionAsync(
            branchId: victimBranch.Id,
            accountId: victimAccount.Id,
            transactionTypeId: victimTransactionType.Id,
            categoryId: victimCategory.Id,
            direction: Direction.In,
            recordedByOperatorId: victimOperator.Id,
            createdByUserId: victimUser.Id);

        var httpResponse = await _client.GetAuthAsync($"/transaction/{victimTransaction.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn400_WhenInstallmentValuesDoNotMatchTotal()
    {
        var (_, _, token) = await SeedInstallmentContextAsync("TxnInstallmentTotalMismatch", SettlementRule.OperatorEnteredCheque);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithValue(301m)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_TOTAL_MISMATCH);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn400_WhenDueDatesAreNotIncreasing()
    {
        var (accountId, transactionTypeId, token) =
            await SeedInstallmentContextAsync("TxnInstallmentDates", SettlementRule.OperatorEnteredCheque);
        var dueDate = DateTime.Today.AddDays(30);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(transactionTypeId)
            .WithAccountId(accountId)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = dueDate,
                    Value = 150m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = dueDate,
                    Value = 150m
                }
            ])
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_DUE_DATES_MUST_INCREASE);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn409_WhenTransactionTypeIsNotCheque()
    {
        var (accountId, transactionTypeId, token) =
            await SeedInstallmentContextAsync("TxnInstallmentNonCheque", SettlementRule.SameDay);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithTransactionTypeId(transactionTypeId)
            .WithAccountId(accountId)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn400_WhenAutoGeneratedSplitCreatesNonPositiveRow()
    {
        var (accountId, transactionTypeId, token) =
            await SeedInstallmentContextAsync("TxnInstallmentSmallAuto", SettlementRule.OperatorEnteredCheque);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithValue(0.10m)
            .WithTransactionTypeId(transactionTypeId)
            .WithAccountId(accountId)
            .WithAutoGeneratedInstallments(6, DateTime.Today.AddDays(30))
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_ROW_VALUE_MUST_BE_POSITIVE);
    }

    private async Task<(Guid AccountId, Guid TransactionTypeId, string Token)> SeedInstallmentContextAsync(
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
