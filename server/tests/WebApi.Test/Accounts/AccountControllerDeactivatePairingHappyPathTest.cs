using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Accounts;

/// <summary>
/// M7.7 Phase 5.3 (decision 1.8, gap S): deactivating either side of a Terminal↔Tab pair
/// atomically unpairs it, so an inactive account never keeps or reserves a pairing.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class AccountControllerDeactivatePairingHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deactivate_ShouldReleaseTheTab_WhenPairedTerminalIsDeactivated()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("PairingDeactivateTerminal", Role.Manager);
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado 01");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal 01", tabAccountId: tab.Id);
        var replacementTerminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal 02");

        var deactivate = await _client.DeleteAuthAsync($"/account/{terminal.Id}", token);

        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivate.Content.ReadAsStringAsync());
        (await deactivate.ReadContentAsync<ResponseAccountJson>()).TabAccountId.ShouldBeNull();
        var persistedTerminal = await factory.ReloadAsync<Account>(terminal.Id);
        persistedTerminal.ShouldNotBeNull();
        persistedTerminal.Active.ShouldBeFalse();
        persistedTerminal.TabAccountId.ShouldBeNull();

        var tabRead = await _client.GetAuthAsync($"/account/{tab.Id}", token);
        tabRead.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tabRead.ReadContentAsync<ResponseAccountJson>()).TerminalAccountId.ShouldBeNull();

        // The released Tab is no longer reserved by the inactive Terminal: it pairs again.
        var repair = await _client.PostAuthAsync(
            "/account/pair-tab",
            new RequestPairTabAccountJson { TerminalAccountId = replacementTerminal.Id, TabAccountId = tab.Id },
            token);

        repair.StatusCode.ShouldBe(HttpStatusCode.OK, await repair.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Account>(replacementTerminal.Id))!.TabAccountId.ShouldBe(tab.Id);
    }

    [Fact]
    public async Task Deactivate_ShouldUnpairTheTerminal_WhenPairedTabIsDeactivated()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("PairingDeactivateTab", Role.Admin);
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado 03");
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal 03", tabAccountId: tab.Id);
        var replacementTab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado 04");

        var deactivate = await _client.DeleteAuthAsync($"/account/{tab.Id}", token);

        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivate.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Account>(tab.Id))!.Active.ShouldBeFalse();
        var persistedTerminal = await factory.ReloadAsync<Account>(terminal.Id);
        persistedTerminal.ShouldNotBeNull();
        persistedTerminal.Active.ShouldBeTrue();
        persistedTerminal.TabAccountId.ShouldBeNull();

        var terminalRead = await _client.GetAuthAsync($"/account/{terminal.Id}", token);
        terminalRead.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await terminalRead.ReadContentAsync<ResponseAccountJson>()).TabAccountId.ShouldBeNull();

        // The still-active Terminal can take a new Tab.
        var repair = await _client.PostAuthAsync(
            "/account/pair-tab",
            new RequestPairTabAccountJson { TerminalAccountId = terminal.Id, TabAccountId = replacementTab.Id },
            token);

        repair.StatusCode.ShouldBe(HttpStatusCode.OK, await repair.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Account>(terminal.Id))!.TabAccountId.ShouldBe(replacementTab.Id);
    }

    [Fact]
    public async Task Deactivate_ShouldLeaveOtherPairsUntouched_WhenOnePairIsReleased()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("PairingDeactivateIsolation", Role.Manager);
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, tabAccountId: tab.Id);
        var otherTab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var otherTerminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, tabAccountId: otherTab.Id);
        var (_, otherBranch, _, _) = await factory.SeedFullBranchContextAsync("PairingDeactivateIsolationOther", Role.Manager);
        var foreignTab = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Tab);
        var foreignTerminal = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal, tabAccountId: foreignTab.Id);

        var deactivate = await _client.DeleteAuthAsync($"/account/{tab.Id}", token);

        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivate.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Account>(terminal.Id))!.TabAccountId.ShouldBeNull();
        (await factory.ReloadAsync<Account>(otherTerminal.Id))!.TabAccountId.ShouldBe(otherTab.Id);
        (await factory.ReloadAsync<Account>(foreignTerminal.Id))!.TabAccountId.ShouldBe(foreignTab.Id);
    }
}
