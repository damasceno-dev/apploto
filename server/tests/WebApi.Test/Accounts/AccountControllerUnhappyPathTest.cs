using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Accounts;

[Collection(ServerApiCollection.Name)]
public class AccountControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateBank_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateBankAccountJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/account/bank", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/account");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task CreateBank_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountBankForbidden", Role.Member);
        var request = new RequestCreateBankAccountJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/account/bank", request, memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task CreateTerminal_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountTerminalForbidden", Role.Member);
        var request = new RequestCreateTerminalAccountJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/account/terminal", request, memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task CreateTab_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountTabForbidden", Role.Member);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(terminal.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/tab", request, memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task PairTab_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountPairForbidden", Role.Member);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminal.Id)
            .WithTabAccountId(tab.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/pair-tab", request, memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task UnpairTab_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountUnpairForbidden", Role.Member);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var httpResponse = await _client.DeleteAuthAsync($"/account/terminal/{terminal.Id}/tab", memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task List_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountListForbidden", Role.Member);

        var httpResponse = await _client.GetAuthAsync("/account", memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountGetForbidden", Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);

        var httpResponse = await _client.GetAuthAsync($"/account/{account.Id}", memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountUpdateForbidden", Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/account/{account.Id}", request, memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("AccountDeactivateForbidden", Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);

        var httpResponse = await _client.DeleteAuthAsync($"/account/{account.Id}", memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task CreateTerminal_ShouldReturn400_WhenRequestAsksForExistingAndNewTab()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("AccountTerminalInvalid");
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(Guid.NewGuid())
            .WithCreateTabAccount(true)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/terminal", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_CREATE_CANNOT_USE_EXISTING_AND_NEW_TAB);
    }

    [Fact]
    public async Task CreateTerminal_ShouldReturn404_WhenExistingTabBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("AccountTerminalCreateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("AccountTerminalCreateVictim");
        var victimTab = await factory.SeedAccountAsync(victimBranch.Id, AccountType.Tab);
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(victimTab.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/terminal", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
    }

    [Fact]
    public async Task CreateTerminal_ShouldReturn409_WhenExistingTabAlreadyLinked()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountTerminalCreateConflict");
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        _ = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, tabAccountId: tab.Id);
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(tab.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/terminal", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
    }

    [Fact]
    public async Task CreateTab_ShouldReturn404_WhenTerminalBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("AccountTabCreateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("AccountTabCreateVictim");
        var victimTerminal = await factory.SeedAccountAsync(victimBranch.Id, AccountType.Terminal);
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(victimTerminal.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/tab", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_NOT_FOUND);
    }

    [Fact]
    public async Task CreateTab_ShouldReturn409_WhenTerminalAlreadyHasTab()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountTabCreateConflict");
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, tabAccountId: tab.Id);
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(terminal.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/tab", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_ALREADY_LINKED);
    }

    [Fact]
    public async Task PairTab_ShouldReturn404_WhenTabBelongsToDifferentBranch()
    {
        var (_, branch, _, attackerToken) = await factory.SeedFullBranchContextAsync("AccountPairAttacker");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("AccountPairVictim");
        var victimTab = await factory.SeedAccountAsync(victimBranch.Id, AccountType.Tab);
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminal.Id)
            .WithTabAccountId(victimTab.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/pair-tab", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
    }

    [Fact]
    public async Task PairTab_ShouldReturn409_WhenTabAlreadyBelongsToAnotherTerminal()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountPairConflict");
        var targetTerminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        _ = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, tabAccountId: tab.Id);
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(targetTerminal.Id)
            .WithTabAccountId(tab.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/pair-tab", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
    }

    [Fact]
    public async Task UnpairTab_ShouldReturn404_WhenTerminalBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("AccountUnpairAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("AccountUnpairVictim");
        var victimTerminal = await factory.SeedAccountAsync(victimBranch.Id, AccountType.Terminal);

        var httpResponse = await _client.DeleteAuthAsync($"/account/terminal/{victimTerminal.Id}/tab", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("AccountGetAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("AccountGetVictim");
        var victimAccount = await factory.SeedAccountAsync(victimBranch.Id, AccountType.BankAccount);

        var httpResponse = await _client.GetAuthAsync($"/account/{victimAccount.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountUpdateInvalid");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var request = new RequestUpdateAccountJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/account/{account.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("AccountUpdateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("AccountUpdateVictim");
        var victimAccount = await factory.SeedAccountAsync(victimBranch.Id, AccountType.BankAccount);
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/account/{victimAccount.Id}", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("AccountDeactivateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("AccountDeactivateVictim");
        var victimAccount = await factory.SeedAccountAsync(victimBranch.Id, AccountType.BankAccount);

        var httpResponse = await _client.DeleteAuthAsync($"/account/{victimAccount.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var persisted = await factory.ReloadAsync<Account>(victimAccount.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage httpResponse)
    {
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }
}
