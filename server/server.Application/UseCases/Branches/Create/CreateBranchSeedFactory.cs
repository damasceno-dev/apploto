using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Branches.Create;

public static class CreateBranchSeedFactory
{
    private const decimal DefaultDailyTargetHours = 7.33m;
    private const decimal DefaultLunchDeductionOver6H = 1.00m;
    private const decimal DefaultLunchDeductionOver4H = 0.25m;

    private static readonly (string Name, Direction DefaultDirection)[] CategorySeeds =
    [
        ("Receita", Direction.In),
        ("Crédito Banco", Direction.In),
        ("Entradas", Direction.In),
        ("Despesas Administrativas", Direction.Out),
        ("Despesas Comerciais", Direction.Out),
        ("Despesas Pessoal", Direction.Out),
        ("Despesas Financeiras", Direction.Out),
        ("Débito Banco", Direction.Out),
        ("Saídas", Direction.Out)
    ];

    private static readonly (string Name, string CategoryName)[] TransactionTypeSeeds =
    [
        ("Cliente", "Saídas"),
        ("Depósito Dinheiro", "Saídas"),
        ("Cartão de Crédito", "Saídas"),
        ("MarketPlace", "Saídas"),
        ("Sobra de Bolão", "Despesas Comerciais"),
        ("Sobra de Federal", "Despesas Comerciais"),
        ("Depósito Cheque", "Saídas"),
        ("PIX", "Saídas"),
        ("Cartão de Débito", "Saídas"),
        ("Telesena", "Saídas"),
        ("Troca de Telesena", "Saídas"),
        ("Raspadinha", "Saídas"),
        ("Encalhe Federal", "Saídas"),
        ("Cliente", "Entradas"),
        ("Pgto Prêmio", "Saídas"),
        ("Desconto", "Despesas Comerciais"),
        ("Volante rejeitado", "Despesas Comerciais"),
        ("Tarifa cartão", "Despesas Comerciais"),
        ("Outras Despesas", "Despesas Comerciais")
    ];

    private static readonly (string Name, int DisplayOrder)[] ProductSeeds =
    [
        ("Telesena", 1),
        ("Raspadinha", 2),
        ("Jogos", 3),
        ("Loteria Especial", 4),
        ("Dinheiro", 5),
        ("Tarifa Bolão", 6),
        ("Federal", 7),
        ("Diferença Caixa", 8)
    ];

    public static IReadOnlyList<Category> CreateDefaultCategories(Guid branchId)
    {
        return CategorySeeds
            .Select(seed => new Category
            {
                Name = seed.Name,
                DefaultDirection = seed.DefaultDirection,
                BranchId = branchId
            })
            .ToList();
    }

    public static IReadOnlyList<TransactionType> CreateDefaultTransactionTypes(IEnumerable<Category> categories)
    {
        var categoriesByName = categories.ToDictionary(category => category.Name);

        return TransactionTypeSeeds
            .Select(seed => new TransactionType
            {
                Name = seed.Name,
                CategoryId = categoriesByName[seed.CategoryName].Id,
                SettlementRule = ResolveSettlementRule(seed.Name),
                RequiresTabAccountAndClient = seed.Name == "Cliente"
            })
            .ToList();
    }

    private static SettlementRule ResolveSettlementRule(string transactionTypeName)
    {
        return transactionTypeName switch
        {
            "Depósito Cheque" => SettlementRule.OperatorEnteredCheque,
            "PIX" => SettlementRule.SameDay,
            "Cartão de Débito" => SettlementRule.NextBusinessDay,
            "Cartão de Crédito" => SettlementRule.TwoBusinessDays,
            "Depósito Dinheiro" => SettlementRule.NextCalendarDay,
            _ => SettlementRule.SameDay
        };
    }

    public static IReadOnlyList<Product> CreateDefaultProducts(Guid branchId)
    {
        return ProductSeeds
            .Select(seed => new Product
            {
                Name = seed.Name,
                DisplayOrder = seed.DisplayOrder,
                BranchId = branchId
            })
            .ToList();
    }

    public static Setting CreateDefaultSetting(Guid branchId)
    {
        return new Setting
        {
            BranchId = branchId,
            LockDate = DateTime.MinValue,
            DailyTargetHours = DefaultDailyTargetHours,
            LunchDeductionOver6H = DefaultLunchDeductionOver6H,
            LunchDeductionOver4H = DefaultLunchDeductionOver4H
        };
    }
}
