using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using WebApi.Test.Infrastructure;

namespace WebApi.Test.Transactions;

/// <summary>
/// Shared setup for the M7.7 Phase 5 physical fiado tests: a Terminal paired to a Tab with an open Draft close for
/// branch-local today, a client, a fiado sale type, counted-cash and non-cash repayment methods, the two system
/// counterpart types, and a caller of the requested role (a Member gets only the grants asked for).
/// </summary>
internal sealed class FiadoLinkedMovementTestContext
{
    private readonly ServerWebApplicationFactory _factory;

    private FiadoLinkedMovementTestContext(ServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Guid BranchId { get; private init; }
    public Guid UserId { get; private init; }
    public Guid OperatorId { get; private init; }
    public Guid TerminalId { get; private init; }
    public Guid TabId { get; private init; }
    public Guid ClientId { get; private init; }
    public Guid SaleTypeId { get; private init; }
    public Guid SaleCategoryId { get; private init; }
    public Guid CashMethodId { get; private init; }
    public Guid NonCashMethodId { get; private init; }
    public Guid UnconfiguredRepaymentTypeId { get; private init; }
    public Guid StockSaleSystemTypeId { get; private init; }
    public Guid CashRepaymentSystemTypeId { get; private init; }
    public Guid CloseId { get; private init; }
    public string Token { get; private init; } = string.Empty;
    public DateTime Today { get; private init; }

    public static async Task<FiadoLinkedMovementTestContext> SeedAsync(
        ServerWebApplicationFactory factory,
        string label,
        Role role,
        bool grantTab = true,
        bool grantTerminal = true,
        bool openClose = true,
        bool pairTab = true)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, role);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado Caixa 1");
        var terminal = await factory.SeedAccountAsync(
            branch.Id, AccountType.Terminal, "Caixa 1", tabAccountId: pairTab ? tab.Id : null);
        if (grantTerminal)
            await factory.SeedOperatorAccountAsync(op.Id, terminal.Id, isPrimary: true);
        if (grantTab)
            await factory.SeedOperatorAccountAsync(op.Id, tab.Id);
        var client = await factory.SeedClientAsync(branch.Id, "Seu Jorge");
        var saleCategory = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var receiptCategory = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var sale = await factory.SeedTransactionTypeAsync(saleCategory.Id, "Cliente", requiresTabAccountAndClient: true);
        var cash = await factory.SeedTransactionTypeAsync(
            receiptCategory.Id, "Recebimento fiado — dinheiro", requiresTabAccountAndClient: true,
            fiadoPaymentKind: FiadoPaymentKind.CountedCash);
        var nonCash = await factory.SeedTransactionTypeAsync(
            receiptCategory.Id, "Recebimento fiado — PIX", requiresTabAccountAndClient: true,
            fiadoPaymentKind: FiadoPaymentKind.NonCash);
        var unconfigured = await factory.SeedTransactionTypeAsync(
            receiptCategory.Id, "Cliente", requiresTabAccountAndClient: true);
        var (stockSale, cashRepayment) = await factory.SeedFiadoSystemTypesAsync(branch.Id);
        var today = new BranchClock().LocalBusinessDate(DateTime.UtcNow);
        var closeId = Guid.Empty;
        if (openClose)
            closeId = (await factory.SeedDailyCloseAsync(branch.Id, terminal.Id, today, DailyCloseStatus.Draft, itemsRecorded: false)).Id;

        return new FiadoLinkedMovementTestContext(factory)
        {
            BranchId = branch.Id,
            UserId = user.Id,
            OperatorId = op.Id,
            TerminalId = terminal.Id,
            TabId = tab.Id,
            ClientId = client.Id,
            SaleTypeId = sale.Id,
            SaleCategoryId = saleCategory.Id,
            CashMethodId = cash.Id,
            NonCashMethodId = nonCash.Id,
            UnconfiguredRepaymentTypeId = unconfigured.Id,
            StockSaleSystemTypeId = stockSale.Id,
            CashRepaymentSystemTypeId = cashRepayment.Id,
            CloseId = closeId,
            Token = token,
            Today = today
        };
    }

    public async Task<IReadOnlyList<Transaction>> ListBranchTransactionsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.Transactions.AsNoTracking().Where(t => t.BranchId == BranchId).ToListAsync();
    }

    public async Task SetCloseStatusAsync(DailyCloseStatus status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.DailyCloses
            .Where(close => close.Id == CloseId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(close => close.Status, status));
    }

    public async Task ExecuteSqlAsync(string sql)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }
}
