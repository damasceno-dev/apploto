using server.Application.Services.DailyCloses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using WebApi.Test.Infrastructure;
using Operator = server.Domain.Entities.Operator;

namespace WebApi.Test.Sessions;

internal sealed record SessionActor(User User, BranchUser Membership, Operator? Operator, string Token);

/// <summary>
/// One branch with a cashbox on its own physical terminal, two granted Members (each linked to an operator), a
/// Manager without an operator link, the counted products and an ordinary SameDay inflow type.
/// </summary>
internal sealed record SessionScenario(
    Branch Branch,
    Account Cashbox,
    Product Cash,
    Product Stock,
    Product CashVariance,
    SessionActor MemberA,
    SessionActor MemberB,
    SessionActor Manager,
    Category InCategory,
    TransactionType InType)
{
    public static async Task<SessionScenario> CreateAsync(ServerWebApplicationFactory factory, string label)
    {
        var (managerUser, branch, managerMembership, managerToken) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var cashbox = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, name: $"Caixa {label}");
        var cash = await factory.SeedProductAsync(branch.Id, "Dinheiro", displayOrder: 1);
        var stock = await factory.SeedProductAsync(branch.Id, "Raspadinha", displayOrder: 2);
        var cashVariance = await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        var category = await factory.SeedCategoryAsync(branch.Id, $"Entradas {label}", Direction.In);
        var type = await factory.SeedTransactionTypeAsync(category.Id, $"Recebimento {label}");

        return new SessionScenario(
            branch,
            cashbox,
            cash,
            stock,
            cashVariance,
            await MemberAsync(factory, branch, cashbox, "A"),
            await MemberAsync(factory, branch, cashbox, "B"),
            new SessionActor(managerUser, managerMembership, null, managerToken),
            category,
            type);
    }

    public async Task<Account> SecondCashboxAsync(ServerWebApplicationFactory factory, Guid terminalId)
    {
        var cashbox = await factory.SeedAccountAsync(Branch.Id, AccountType.Terminal, name: $"Caixa 2 {Branch.Name}");
        await factory.ExecuteSqlAsync($"UPDATE \"Accounts\" SET \"TerminalId\" = '{terminalId}' WHERE \"Id\" = '{cashbox.Id}'");
        foreach (var member in new[] { MemberA, MemberB })
            await factory.SeedOperatorAccountAsync(member.Operator!.Id, cashbox.Id);
        return cashbox;
    }

    private static async Task<SessionActor> MemberAsync(
        ServerWebApplicationFactory factory,
        Branch branch,
        Account cashbox,
        string suffix)
    {
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, $"Operador {suffix}", user.Id);
        await factory.SeedOperatorAccountAsync(op.Id, cashbox.Id);
        return new SessionActor(user, membership, op, factory.IssueBranchToken(membership));
    }
}
