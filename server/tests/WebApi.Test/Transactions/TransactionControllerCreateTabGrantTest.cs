using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 5.3 (decision 1.8, gaps L + S): a Terminal assignment grants till operation only.
/// A Member records fiado on a Tab only through an explicit <c>OperatorAccount(Tab)</c> grant —
/// never implied by the paired Terminal — and may hold several Tab grants across terminals.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionControllerCreateTabGrantTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldReturn403_WhenJuniorHoldsOnlyThePairedTerminal()
    {
        var ctx = await SeedContext("TabGrantJunior");
        await factory.SeedOperatorAccountAsync(ctx.OperatorId, ctx.TerminalOneId, isPrimary: true);

        var response = await CreateFiado(ctx, ctx.TabOneId);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        (await CountTransactionsAsync(ctx.BranchId)).ShouldBe(0);
    }

    [Fact]
    public async Task Create_ShouldRecordOnEveryGrantedTab_WhenSeniorHoldsTwoTerminalsTabs()
    {
        var ctx = await SeedContext("TabGrantSenior");
        await factory.SeedOperatorAccountAsync(ctx.OperatorId, ctx.TerminalOneId, isPrimary: true);
        await factory.SeedOperatorAccountAsync(ctx.OperatorId, ctx.TabOneId);
        await factory.SeedOperatorAccountAsync(ctx.OperatorId, ctx.TabTwoId);

        var first = await CreateFiado(ctx, ctx.TabOneId);
        var second = await CreateFiado(ctx, ctx.TabTwoId);

        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        second.StatusCode.ShouldBe(HttpStatusCode.Created, await second.Content.ReadAsStringAsync());
        (await first.ReadContentAsync<ResponseTransactionJson>()).AccountId.ShouldBe(ctx.TabOneId);
        (await second.ReadContentAsync<ResponseTransactionJson>()).AccountId.ShouldBe(ctx.TabTwoId);
        (await CountTransactionsAsync(ctx.BranchId)).ShouldBe(2);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenTabGrantWasRemovedByDeactivatingIt()
    {
        var ctx = await SeedContext("TabGrantDeactivated");
        await factory.SeedOperatorAccountAsync(ctx.OperatorId, ctx.TerminalOneId, isPrimary: true);
        await factory.SeedOperatorAccountAsync(ctx.OperatorId, ctx.TabOneId, active: false);

        var response = await CreateFiado(ctx, ctx.TabOneId);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    private async Task<GrantContext> SeedContext(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tabOne = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado 01");
        var terminalOne = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal 01", tabAccountId: tabOne.Id);
        var tabTwo = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado 02");
        await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal 02", tabAccountId: tabTwo.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, requiresTabAccountAndClient: true);
        var client = await factory.SeedClientAsync(branch.Id);
        return new GrantContext(branch.Id, op.Id, terminalOne.Id, tabOne.Id, tabTwo.Id, transactionType.Id, client.Id, token);
    }

    private Task<HttpResponseMessage> CreateFiado(GrantContext ctx, Guid tabAccountId)
    {
        var today = new BranchClock().LocalBusinessDate(DateTime.UtcNow);
        return _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder()
                .WithDate(today)
                .WithValue(5m)
                .WithTransactionTypeId(ctx.TransactionTypeId)
                .WithAccountId(tabAccountId)
                .WithClientId(ctx.ClientId)
                .Build(),
            ctx.Token);
    }

    private async Task<int> CountTransactionsAsync(Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.Transactions.CountAsync(transaction => transaction.BranchId == branchId);
    }

    private sealed record GrantContext(
        Guid BranchId,
        Guid OperatorId,
        Guid TerminalOneId,
        Guid TabOneId,
        Guid TabTwoId,
        Guid TransactionTypeId,
        Guid ClientId,
        string Token);
}
