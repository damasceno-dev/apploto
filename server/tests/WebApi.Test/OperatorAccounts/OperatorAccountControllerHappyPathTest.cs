using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OperatorAccounts;

[Collection(ServerApiCollection.Name)]
public class OperatorAccountControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListAccounts_ShouldReturn200WithAssignedAccounts()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountList", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, "Operator List");
        var primaryAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Primary");
        var secondaryAccount = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Secondary");
        await factory.SeedOperatorAccountAsync(op.Id, primaryAccount.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(op.Id, secondaryAccount.Id);

        var httpResponse = await _client.GetAuthAsync($"/operator/{op.Id}/accounts", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListOperatorAccountsJson>();
        payload.OperatorAccounts.Count.ShouldBe(2);
        payload.OperatorAccounts.ShouldContain(link =>
            link.OperatorId == op.Id &&
            link.AccountId == primaryAccount.Id &&
            link.IsPrimary);
        payload.OperatorAccounts.ShouldContain(link =>
            link.OperatorId == op.Id &&
            link.AccountId == secondaryAccount.Id &&
            !link.IsPrimary);
    }

    [Fact]
    public async Task AssignAccount_ShouldReturn200AndCreateLink_WhenManagerAssignsAccount()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountAssign", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, "Operator Assign");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal Assign");
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/operator/{op.Id}/accounts", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorAccountJson>();
        payload.OperatorId.ShouldBe(op.Id);
        payload.AccountId.ShouldBe(account.Id);
        payload.IsPrimary.ShouldBeFalse();
        payload.AccountName.ShouldBe(account.Name);
        payload.AccountType.ShouldBe(account.Type);

        var links = await factory.ListOperatorAccountsByOperatorIdAsync(op.Id);
        links.Count.ShouldBe(1);
        links[0].Active.ShouldBeTrue();
    }

    [Fact]
    public async Task AssignAccount_ShouldReturn200AndReactivateExistingInactiveLink()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountReactivate");
        var op = await factory.SeedOperatorAsync(branch.Id, "Operator Reactivate");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Bank Reactivate");
        var inactiveLink = await factory.SeedOperatorAccountAsync(op.Id, account.Id, active: false);
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/operator/{op.Id}/accounts", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorAccountJson>();
        payload.Id.ShouldBe(inactiveLink.Id);

        var links = await factory.ListOperatorAccountsByOperatorIdAsync(op.Id);
        links.Count.ShouldBe(1);
        links[0].Id.ShouldBe(inactiveLink.Id);
        links[0].Active.ShouldBeTrue();
    }

    [Fact]
    public async Task UnassignAccount_ShouldReturn200AndClearPrimaryFlag()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountUnassign");
        var op = await factory.SeedOperatorAsync(branch.Id, "Operator Unassign");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal Unassign");
        await factory.SeedOperatorAccountAsync(op.Id, account.Id, isPrimary: true);

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{op.Id}/accounts/{account.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorAccountJson>();
        payload.OperatorId.ShouldBe(op.Id);
        payload.AccountId.ShouldBe(account.Id);
        payload.IsPrimary.ShouldBeFalse();

        var links = await factory.ListOperatorAccountsByOperatorIdAsync(op.Id);
        links.Count.ShouldBe(1);
        links[0].Active.ShouldBeFalse();
        links[0].IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public async Task SetPrimaryAccount_ShouldReturn200AndClearPreviousPrimary()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountSetPrimary", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, "Operator Primary");
        var previousAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Old Primary");
        var nextAccount = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "New Primary");
        await factory.SeedOperatorAccountAsync(op.Id, previousAccount.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(op.Id, nextAccount.Id);

        var httpResponse = await _client.PutAuthAsync(
            $"/operator/{op.Id}/accounts/{nextAccount.Id}/primary",
            new { },
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseOperatorAccountJson>();
        payload.OperatorId.ShouldBe(op.Id);
        payload.AccountId.ShouldBe(nextAccount.Id);
        payload.IsPrimary.ShouldBeTrue();

        var links = await factory.ListOperatorAccountsByOperatorIdAsync(op.Id);
        links.ShouldContain(link => link.AccountId == previousAccount.Id && link.IsPrimary == false);
        links.ShouldContain(link => link.AccountId == nextAccount.Id && link.IsPrimary);
    }

    [Fact]
    public async Task GetSelfContext_ShouldReturn200WithOperatorAndAccounts_WhenCallerIsMember()
    {
        var (user, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OperatorSelfContext", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, "Operator Self", userId: user.Id);
        var primaryAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Primary Self");
        var secondaryAccount = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Secondary Self");
        await factory.SeedOperatorAccountAsync(op.Id, primaryAccount.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(op.Id, secondaryAccount.Id);

        var httpResponse = await _client.GetAuthAsync("/operator/self-context", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseSelfContextJson>();
        payload.OperatorId.ShouldBe(op.Id);
        payload.OperatorName.ShouldBe(op.Name);
        payload.PrimaryAccount.ShouldNotBeNull();
        payload.PrimaryAccount.AccountId.ShouldBe(primaryAccount.Id);
        payload.AvailableAccounts.Count.ShouldBe(2);
        payload.AvailableAccounts.ShouldContain(link => link.AccountId == secondaryAccount.Id);
    }

    [Fact]
    public async Task GetSelfContext_ShouldReturn200WithEmptyContext_WhenMemberHasNoLinkedOperator()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("OperatorSelfContextEmpty", Role.Member);

        var httpResponse = await _client.GetAuthAsync("/operator/self-context", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseSelfContextJson>();
        payload.OperatorId.ShouldBeNull();
        payload.PrimaryAccount.ShouldBeNull();
        payload.AvailableAccounts.ShouldBeEmpty();
    }
}
