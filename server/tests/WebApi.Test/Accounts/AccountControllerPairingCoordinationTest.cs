using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Accounts;

/// <summary>
/// M7.7 Phase 5.3: pairing changes and account deactivation serialize on one branch-scoped
/// advisory key, so a pair racing a deactivation can never leave an inactive account holding a
/// pairing — whichever request the database grants first.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class AccountControllerPairingCoordinationTest(ServerWebApplicationFactory factory)
{
    private static readonly TimeSpan GrantOrderTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task DeactivateTabFirst_ShouldMakeQueuedPairFail_LeavingNoPairingToTheInactiveTab()
    {
        var ctx = await SeedContext("PairingRaceDeactivateFirst");
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var deactivateClient = grantOrderFactory.CreateClient();
        using var pairClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldPairingLockAsync(ctx.BranchId);

        var deactivateTask = deactivateClient.DeleteAuthAsync($"/account/{ctx.TabId}", ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var pairTask = Pair(pairClient, ctx);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(deactivateTask, pairTask).WaitAsync(TimeSpan.FromSeconds(10));
        var deactivateResponse = await deactivateTask;
        var pairResponse = await pairTask;

        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivateResponse.Content.ReadAsStringAsync());
        pairResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await pairResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        (await factory.ReloadAsync<Account>(ctx.TabId))!.Active.ShouldBeFalse();
        (await factory.ReloadAsync<Account>(ctx.TerminalId))!.TabAccountId.ShouldBeNull();
    }

    [Fact]
    public async Task PairFirst_ShouldBeUndoneByQueuedTabDeactivation_LeavingNoPairingToTheInactiveTab()
    {
        var ctx = await SeedContext("PairingRacePairFirst");
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var pairClient = grantOrderFactory.CreateClient();
        using var deactivateClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldPairingLockAsync(ctx.BranchId);

        var pairTask = Pair(pairClient, ctx);
        await heldLock.WaitForWaitersAsync(1);
        var deactivateTask = deactivateClient.DeleteAuthAsync($"/account/{ctx.TabId}", ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(pairTask, deactivateTask).WaitAsync(TimeSpan.FromSeconds(10));
        var pairResponse = await pairTask;
        var deactivateResponse = await deactivateTask;

        pairResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pairResponse.Content.ReadAsStringAsync());
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivateResponse.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Account>(ctx.TabId))!.Active.ShouldBeFalse();
        (await factory.ReloadAsync<Account>(ctx.TerminalId))!.TabAccountId.ShouldBeNull();
    }

    [Fact]
    public async Task DeactivateTerminalFirst_ShouldMakeQueuedPairFail_LeavingTheTabFree()
    {
        var ctx = await SeedContext("PairingRaceTerminalFirst");
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var deactivateClient = grantOrderFactory.CreateClient();
        using var pairClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldPairingLockAsync(ctx.BranchId);

        var deactivateTask = deactivateClient.DeleteAuthAsync($"/account/{ctx.TerminalId}", ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var pairTask = Pair(pairClient, ctx);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(deactivateTask, pairTask).WaitAsync(TimeSpan.FromSeconds(10));
        var deactivateResponse = await deactivateTask;
        var pairResponse = await pairTask;

        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivateResponse.Content.ReadAsStringAsync());
        pairResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await pairResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_NOT_FOUND);
        var terminal = await factory.ReloadAsync<Account>(ctx.TerminalId);
        terminal!.Active.ShouldBeFalse();
        terminal.TabAccountId.ShouldBeNull();
    }

    [Fact]
    public async Task Pair_ShouldReturnExactCoordinationBusyKey_WhenPostgresBoundaryTimesOut()
    {
        var ctx = await SeedContext("PairingBusy");
        await using var busyFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<AccountPairingCoordinationOptions>();
            services.AddSingleton(new AccountPairingCoordinationOptions(TimeSpan.FromMilliseconds(100)));
            services.RemoveAll<DailyCloseAccountCoordinationOptions>();
            services.AddSingleton(new DailyCloseAccountCoordinationOptions(TimeSpan.FromMilliseconds(100)));
        }));
        using var client = busyFactory.CreateClient();
        await using var heldLock = await HoldPairingLockAsync(ctx.BranchId);

        var pairResponse = await Pair(client, ctx);
        var deactivateResponse = await client.DeleteAuthAsync($"/account/{ctx.TabId}", ctx.Token);

        // Pairing and deactivation now wait on the pairing key inside the §6.20 account coordination (M9.6 Phase 2), so a
        // held pairing key surfaces as that coordination's retryable busy key.
        pairResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict, await pairResponse.Content.ReadAsStringAsync());
        (await pairResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await deactivateResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY);
        (await factory.ReloadAsync<Account>(ctx.TerminalId))!.TabAccountId.ShouldBeNull();
        (await factory.ReloadAsync<Account>(ctx.TabId))!.Active.ShouldBeTrue();
    }

    private WebApplicationFactory<Program> CreateGrantOrderFactory()
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<AccountPairingCoordinationOptions>();
            services.AddSingleton(new AccountPairingCoordinationOptions(GrantOrderTimeout));
        }));
    }

    private async Task<PairingContext> SeedContext(string label)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        return new PairingContext(branch.Id, terminal.Id, tab.Id, token);
    }

    private static Task<HttpResponseMessage> Pair(HttpClient client, PairingContext ctx)
    {
        return client.PostAuthAsync(
            "/account/pair-tab",
            new RequestPairTabAccountJson { TerminalAccountId = ctx.TerminalId, TabAccountId = ctx.TabId },
            ctx.Token);
    }

    private async Task<HeldAdvisoryLock> HoldPairingLockAsync(Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        return await HeldAdvisoryLock.AcquireAsync(connectionString, AccountPairingCoordinationKey.Compute(branchId));
    }

    private sealed record PairingContext(Guid BranchId, Guid TerminalId, Guid TabId, string Token);
}
