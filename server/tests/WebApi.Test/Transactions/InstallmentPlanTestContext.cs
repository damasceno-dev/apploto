using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using WebApi.Test.Infrastructure;

namespace WebApi.Test.Transactions;

/// <summary>
/// Shared setup for the M7.7 Phase 7.6.2 plan tests: a branch with a caller of the given role linked to an
/// operator, a Bank account in that operator's scope (no DailyClose prerequisite), and a cheque-rule type.
/// Seeded plans follow the production shape: the first installment's id is the plan's origin id.
/// </summary>
internal sealed class InstallmentPlanTestContext
{
    private readonly ServerWebApplicationFactory _factory;

    private InstallmentPlanTestContext(ServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public Guid BranchId { get; private init; }
    public Guid UserId { get; private init; }
    public Guid OperatorId { get; private init; }
    public Guid BankAccountId { get; private init; }
    public Guid ChequeTypeId { get; private init; }
    public Guid ChequeCategoryId { get; private init; }
    public string Token { get; private init; } = string.Empty;
    public DateTime Today { get; private init; }

    public static async Task<InstallmentPlanTestContext> SeedAsync(
        ServerWebApplicationFactory factory,
        string label,
        Role role)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, role);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var bank = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        await factory.SeedOperatorAccountAsync(op.Id, bank.Id, isPrimary: true);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var chequeType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.OperatorEnteredCheque);
        return new InstallmentPlanTestContext(factory)
        {
            BranchId = branch.Id,
            UserId = user.Id,
            OperatorId = op.Id,
            BankAccountId = bank.Id,
            ChequeTypeId = chequeType.Id,
            ChequeCategoryId = category.Id,
            Token = token,
            Today = new BranchClock().LocalBusinessDate(DateTime.UtcNow)
        };
    }

    /// <summary>Seeds one R$ 100 installment per status, due 30 days apart; returns the plan's origin id.</summary>
    public async Task<Guid> SeedPlanAsync(params TransactionStatus[] statuses)
    {
        return await SeedPlanOnAsync(BankAccountId, Today, statuses);
    }

    public async Task<Guid> SeedPlanOnAsync(Guid accountId, DateTime date, params TransactionStatus[] statuses)
    {
        var originId = Guid.NewGuid();
        for (var index = 0; index < statuses.Length; index++)
        {
            await _factory.SeedTransactionAsync(
                BranchId,
                accountId,
                ChequeTypeId,
                ChequeCategoryId,
                Direction.Out,
                OperatorId,
                UserId,
                date: date,
                value: 100m,
                description: $"CH PRE ({index + 1}/{statuses.Length})",
                dueDate: date.AddDays(30 * (index + 1)),
                originTransactionId: originId,
                status: statuses[index],
                id: index == 0 ? originId : Guid.NewGuid());
        }

        return originId;
    }

    public async Task<Transaction> SeedStandaloneDraftAsync()
    {
        var seeded = await _factory.SeedTransactionAsync(
            BranchId,
            BankAccountId,
            ChequeTypeId,
            ChequeCategoryId,
            Direction.Out,
            OperatorId,
            UserId,
            date: Today,
            value: 50m,
            dueDate: Today.AddDays(10),
            status: TransactionStatus.Draft);
        return (await _factory.ReloadAsync<Transaction>(seeded.Id))!;
    }
}
