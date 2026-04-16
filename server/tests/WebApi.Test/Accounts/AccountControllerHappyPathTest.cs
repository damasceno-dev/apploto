using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Accounts;

[Collection(ServerApiCollection.Name)]
public class AccountControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateBank_ShouldReturn201AndPersistBankAccount()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountBankCreate", Role.Manager);
        var request = new RequestCreateBankAccountJsonBuilder()
            .WithName("Main Bank")
            .WithInstitution("Inter")
            .WithNumber("1234")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/bank", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateAccountJson>();
        payload.Type.ShouldBe(AccountType.BankAccount);
        payload.Name.ShouldBe(request.Name);
        payload.Institution.ShouldBe(request.Institution);
        payload.Number.ShouldBe(request.Number);
        payload.BranchId.ShouldBe(branch.Id);
        payload.TabAccountId.ShouldBeNull();
        payload.TerminalAccountId.ShouldBeNull();

        var persisted = await factory.ReloadAsync<Account>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.Type.ShouldBe(AccountType.BankAccount);
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTerminal_ShouldReturn201AndCreateTab_WhenRequested()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountTerminalCreate");
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithName("Terminal Alpha")
            .WithInstitution("Stone")
            .WithNumber("T-01")
            .WithCreateTabAccount(true)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/terminal", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateAccountJson>();
        payload.Type.ShouldBe(AccountType.Terminal);
        payload.BranchId.ShouldBe(branch.Id);
        payload.TabAccountId.ShouldNotBeNull();

        var terminal = await factory.ReloadAsync<Account>(payload.Id);
        var tab = await factory.ReloadAsync<Account>(payload.TabAccountId!.Value);
        terminal.ShouldNotBeNull();
        tab.ShouldNotBeNull();
        terminal.TabAccountId.ShouldBe(tab.Id);
        tab.Type.ShouldBe(AccountType.Tab);
        tab.Name.ShouldBe(request.Name);
        tab.Institution.ShouldBe(request.Institution);
        tab.Number.ShouldBe(request.Number);
        tab.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task CreateTab_ShouldReturn201AndMirrorTerminalFields()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountTabCreate");
        var terminal = await factory.SeedAccountAsync(
            branch.Id,
            AccountType.Terminal,
            "Terminal Mirror",
            "PagSeguro",
            "TERM-99");
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(terminal.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/tab", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateAccountJson>();
        payload.Type.ShouldBe(AccountType.Tab);
        payload.TerminalAccountId.ShouldBe(terminal.Id);
        payload.Name.ShouldBe(terminal.Name);
        payload.Institution.ShouldBe(terminal.Institution);
        payload.Number.ShouldBe(terminal.Number);

        var updatedTerminal = await factory.ReloadAsync<Account>(terminal.Id);
        var tab = await factory.ReloadAsync<Account>(payload.Id);
        updatedTerminal.ShouldNotBeNull();
        tab.ShouldNotBeNull();
        updatedTerminal.TabAccountId.ShouldBe(tab.Id);
        tab.Type.ShouldBe(AccountType.Tab);
        tab.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task PairTab_ShouldReturn200AndLinkExistingTab()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountPairTab");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "POS 1");
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Tab 1");
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminal.Id)
            .WithTabAccountId(tab.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/account/pair-tab", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseAccountJson>();
        payload.Id.ShouldBe(terminal.Id);
        payload.TabAccountId.ShouldBe(tab.Id);

        var persisted = await factory.ReloadAsync<Account>(terminal.Id);
        persisted.ShouldNotBeNull();
        persisted.TabAccountId.ShouldBe(tab.Id);
    }

    [Fact]
    public async Task UnpairTab_ShouldReturn200AndClearPairing()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountUnpairTab");
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Open Tab");
        var terminal = await factory.SeedAccountAsync(
            branch.Id,
            AccountType.Terminal,
            "POS 2",
            tabAccountId: tab.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/account/terminal/{terminal.Id}/tab", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseAccountJson>();
        payload.Id.ShouldBe(terminal.Id);
        payload.TabAccountId.ShouldBeNull();

        var persisted = await factory.ReloadAsync<Account>(terminal.Id);
        persisted.ShouldNotBeNull();
        persisted.TabAccountId.ShouldBeNull();
    }

    [Fact]
    public async Task List_ShouldReturn200WithDerivedTerminalDataForTabAccounts()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountList", Role.Manager);
        
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Shared Tab");
        var terminal = await factory.SeedAccountAsync(branch.Id,AccountType.Terminal,"POS List",tabAccountId: tab.Id);
        var bank = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Vault Bank");

        var httpResponse = await _client.GetAuthAsync("/account", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListAccountsJson>();
        
        payload.Accounts.ShouldContain(account =>account.Id == terminal.Id &&account.Type == AccountType.Terminal &&account.TabAccountId == tab.Id);
        payload.Accounts.ShouldContain(account =>account.Id == tab.Id &&account.Type == AccountType.Tab &&account.TerminalAccountId == terminal.Id);
        payload.Accounts.ShouldContain(account =>account.Id == bank.Id &&account.Type == AccountType.BankAccount);
    }

    [Fact]
    public async Task Get_ShouldReturn200WithDerivedTerminalData_WhenAccountIsTab()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountGet");
        
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Tab Get");
        var terminal = await factory.SeedAccountAsync(branch.Id,AccountType.Terminal,"POS Get",tabAccountId: tab.Id);

        var httpResponse = await _client.GetAuthAsync($"/account/{tab.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseAccountJson>();
        payload.Id.ShouldBe(tab.Id);
        payload.Type.ShouldBe(AccountType.Tab);
        payload.TerminalAccountId.ShouldBe(terminal.Id);
    }

    [Fact]
    public async Task Update_ShouldReturn200AndPersistDescriptiveFields()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountUpdate", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Old Name", "Old Bank", "001");
        var request = new RequestUpdateAccountJsonBuilder()
            .WithName("Updated Name")
            .WithInstitution("Updated Bank")
            .WithNumber("999")
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/account/{account.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseAccountJson>();
        payload.Id.ShouldBe(account.Id);
        payload.Name.ShouldBe(request.Name);
        payload.Institution.ShouldBe(request.Institution);
        payload.Number.ShouldBe(request.Number);

        var persisted = await factory.ReloadAsync<Account>(account.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe(request.Name);
        persisted.Institution.ShouldBe(request.Institution);
        persisted.Number.ShouldBe(request.Number);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn200AndCascadeDeactivateOperatorLinks()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("AccountDeactivate");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "POS Close");
        var operatorOne = await factory.SeedOperatorAsync(branch.Id, "Operator One");
        var operatorTwo = await factory.SeedOperatorAsync(branch.Id, "Operator Two");
        await factory.SeedOperatorAccountAsync(operatorOne.Id, account.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(operatorTwo.Id, account.Id, isPrimary: true);

        var httpResponse = await _client.DeleteAuthAsync($"/account/{account.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseAccountJson>();
        payload.Id.ShouldBe(account.Id);

        var persistedAccount = await factory.ReloadAsync<Account>(account.Id);
        var links = await factory.ListOperatorAccountsByAccountIdAsync(account.Id);
        persistedAccount.ShouldNotBeNull();
        persistedAccount.Active.ShouldBeFalse();
        links.Count.ShouldBe(2);
        links.All(link => link.Active is false).ShouldBeTrue();
        links.All(link => link.IsPrimary is false).ShouldBeTrue();
    }
}
