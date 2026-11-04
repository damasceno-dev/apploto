using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models;

namespace server.Application.UseCases.Branches.Create;

public static class CreateBranchSeedFactory
{
    private const decimal DefaultDailyTargetHours = 7.33m;
    private const decimal DefaultLunchDeductionOver6H = 1.00m;
    private const decimal DefaultLunchDeductionOver4H = 0.25m;

    private static readonly (string Name, Direction DefaultDirection)[] CategorySeeds =
    [
        (ReceitaCategoryName, Direction.In),
        ("Crédito Banco", Direction.In),
        (EntradasCategoryName, Direction.In),
        (DespesasAdministrativasCategoryName, Direction.Out),
        ("Despesas Comerciais", Direction.Out),
        (DespesasPessoalCategoryName, Direction.Out),
        (DespesasFinanceirasCategoryName, Direction.Out),
        ("Débito Banco", Direction.Out),
        (SaidasCategoryName, Direction.Out),
        (FiadoAutomaticOutCategoryName, Direction.Out),
        (FiadoAutomaticInCategoryName, Direction.In),
        (SafeAutomaticInCategoryName, Direction.In)
    ];

    public const string ReceitaCategoryName = "Receita";
    public const string EntradasCategoryName = "Entradas";
    public const string SaidasCategoryName = "Saídas";
    public const string DespesasAdministrativasCategoryName = "Despesas Administrativas";
    public const string DespesasPessoalCategoryName = "Despesas Pessoal";
    public const string DespesasFinanceirasCategoryName = "Despesas Financeiras";

    /// <summary>
    /// M7.7 Phase 5.6 / M9.6 Phase 9: the generated fiado and cofre legs live in their own system categories, so they
    /// are classified apart from ordinary sales and receipts; such a category is read-only.
    /// </summary>
    public const string FiadoAutomaticOutCategoryName = "Movimentos automáticos de fiado (saída)";
    public const string FiadoAutomaticInCategoryName = "Movimentos automáticos de fiado (entrada)";
    public const string SafeAutomaticInCategoryName = "Movimentos automáticos de cofre (entrada)";
    public const string FiadoSaleCounterpartTypeName = "Saída de caixa — venda fiado";
    public const string FiadoCashRepaymentCounterpartTypeName = "Entrada de dinheiro — recebimento fiado";
    public const string SafeDepositInTypeName = "Entrada no cofre — depósito do caixa";

    /// <summary>
    /// M9.7 Phase 2.3: the Tab <c>Out</c> written only by <i>Converter em fiado</i>, in the automatic fiado (saída)
    /// category beside the counter-sale leg.
    /// </summary>
    public const string ChequeFiadoConversionTypeName = "Cheque convertido em fiado";

    /// <summary>M9.6 Phases 7 and 9: system types that live in ordinary categories (refused by the generic create).</summary>
    public const string BolaoFeeTypeName = "Tarifa Bolão";
    public const string SpecialFeeTypeName = "Tarifa Especial";
    public const string TerminalReportInTypeName = "Relatório do terminal";
    public const string TerminalReportOutTypeName = "Relatório do terminal (negativo)";
    public const string SafeDepositOutTypeName = "Depósito Dinheiro";

    /// <summary>M9.6 Phase 6: the initial fiado repayment methods. Neither is a default; the method stays a choice.</summary>
    public const string CashRepaymentMethodName = "Dinheiro";
    public const string PixRepaymentMethodName = "PIX";

    private sealed record TypeSeed(
        string Key,
        string Name,
        string CategoryName,
        SettlementRule SettlementRule = SettlementRule.SameDay,
        bool RequiresTabAccountAndClient = false,
        TransactionTypeSystemRole? SystemRole = null,
        FiadoPaymentKind? FiadoPaymentKind = null,
        SettlementInstrument SettlementInstrument = SettlementInstrument.None,
        MonthlyFinancialRole MonthlyFinancialRole = MonthlyFinancialRole.None);

    /// <summary>
    /// M9.6 Phase 9: the 32 drawer expense types of the Access catalog (TblTipoCategoria 29–60) under Despesas
    /// Administrativas (15), Pessoal (11) and Financeiras (6); the four Despesas Comerciais types were already seeded.
    /// Generic names only — they classify an expense and carry no payroll, commission or tax rule.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Name, string CategoryName)> ExpenseSeeds =
    [
        ("administrativas.agua", "Água", DespesasAdministrativasCategoryName),
        ("administrativas.aluguel", "Aluguel", DespesasAdministrativasCategoryName),
        ("administrativas.carro-forte", "Carro Forte", DespesasAdministrativasCategoryName),
        ("administrativas.cel-fixo-internet", "Cel Fixo Internet", DespesasAdministrativasCategoryName),
        ("administrativas.contabilidade", "Contabilidade", DespesasAdministrativasCategoryName),
        ("administrativas.copa-banheiro", "Copa e Banheiro", DespesasAdministrativasCategoryName),
        ("administrativas.energia", "Energia", DespesasAdministrativasCategoryName),
        ("administrativas.escritorio", "Escritório", DespesasAdministrativasCategoryName),
        ("administrativas.juridico", "Jurídico", DespesasAdministrativasCategoryName),
        ("administrativas.marketing", "Marketing", DespesasAdministrativasCategoryName),
        ("administrativas.seguro-tfl", "Seguro TFL", DespesasAdministrativasCategoryName),
        ("administrativas.seguros", "Seguros", DespesasAdministrativasCategoryName),
        ("administrativas.sindicato", "Sindicato", DespesasAdministrativasCategoryName),
        ("administrativas.seguranca", "Segurança", DespesasAdministrativasCategoryName),
        ("administrativas.outras-despesas", "Outras Despesas", DespesasAdministrativasCategoryName),
        ("pessoal.decimo-terceiro", "13º salário", DespesasPessoalCategoryName),
        ("pessoal.comissao", "Comissão", DespesasPessoalCategoryName),
        ("pessoal.ferias", "Férias", DespesasPessoalCategoryName),
        ("pessoal.fgts", "FGTS", DespesasPessoalCategoryName),
        ("pessoal.horas-extras", "Horas extras", DespesasPessoalCategoryName),
        ("pessoal.inss", "INSS", DespesasPessoalCategoryName),
        ("pessoal.pro-labore", "Pró Labore", DespesasPessoalCategoryName),
        ("pessoal.salario-mensal", "Salário mensal", DespesasPessoalCategoryName),
        ("pessoal.vale-transporte", "Vale Transporte", DespesasPessoalCategoryName),
        ("pessoal.vale-alimentacao", "Vale Alimentação", DespesasPessoalCategoryName),
        ("pessoal.outras-despesas", "Outras despesas", DespesasPessoalCategoryName),
        ("financeiras.issqn", "ISSQN", DespesasFinanceirasCategoryName),
        ("financeiras.iptu", "IPTU", DespesasFinanceirasCategoryName),
        ("financeiras.simples-nacional", "Simples Nacional", DespesasFinanceirasCategoryName),
        ("financeiras.taxas-bancarias", "Taxas Bancárias", DespesasFinanceirasCategoryName),
        ("financeiras.juros", "Juros", DespesasFinanceirasCategoryName),
        ("financeiras.outras-despesas", "Outras despesas", DespesasFinanceirasCategoryName)
    ];

    /// <summary>
    /// The fresh-branch type catalog, each row with its stable seed key. Classification is keyed by this catalog's own
    /// rows (M11 Phases 4/6; working answers SQ1–SQ4), never applied to an existing branch's renamed or custom types.
    /// </summary>
    private static readonly TypeSeed[] TransactionTypeSeeds =
    [
        new("receita.tarifa-bolao", BolaoFeeTypeName, ReceitaCategoryName,
            SystemRole: TransactionTypeSystemRole.BolaoFee, MonthlyFinancialRole: MonthlyFinancialRole.BolaoServiceFee),
        new("receita.tarifa-especial", SpecialFeeTypeName, ReceitaCategoryName,
            SystemRole: TransactionTypeSystemRole.SpecialFee, MonthlyFinancialRole: MonthlyFinancialRole.SpecialDrawServiceFee),
        new("entradas.relatorio-terminal", TerminalReportInTypeName, EntradasCategoryName,
            SystemRole: TransactionTypeSystemRole.TerminalReportIn),
        new("saidas.relatorio-terminal-negativo", TerminalReportOutTypeName, SaidasCategoryName,
            SystemRole: TransactionTypeSystemRole.TerminalReportOut),
        new("saidas.cliente", "Cliente", SaidasCategoryName, RequiresTabAccountAndClient: true),
        new("saidas.deposito-dinheiro", SafeDepositOutTypeName, SaidasCategoryName,
            SettlementRule.NextCalendarDay, SystemRole: TransactionTypeSystemRole.SafeDepositOut),
        new("saidas.cartao-credito", "Cartão de Crédito", SaidasCategoryName,
            SettlementRule.TwoBusinessDays, SettlementInstrument: SettlementInstrument.CreditCard),
        new("saidas.marketplace", "MarketPlace", SaidasCategoryName),
        new("comerciais.sobra-bolao", "Sobra de Bolão", "Despesas Comerciais",
            MonthlyFinancialRole: MonthlyFinancialRole.CommercialLeftover),
        new("comerciais.sobra-federal", "Sobra de Federal", "Despesas Comerciais",
            MonthlyFinancialRole: MonthlyFinancialRole.CommercialLeftover),
        new("saidas.deposito-cheque", "Depósito Cheque", SaidasCategoryName,
            SettlementRule.OperatorEnteredCheque, SettlementInstrument: SettlementInstrument.Cheque),
        new("saidas.pix", "PIX", SaidasCategoryName),
        new("saidas.cartao-debito", "Cartão de Débito", SaidasCategoryName,
            SettlementRule.NextBusinessDay, SettlementInstrument: SettlementInstrument.DebitCard),
        new("saidas.telesena", "Telesena", SaidasCategoryName),
        new("saidas.troca-telesena", "Troca de Telesena", SaidasCategoryName),
        new("saidas.raspadinha", "Raspadinha", SaidasCategoryName),
        new("saidas.encalhe-federal", "Encalhe Federal", SaidasCategoryName),
        new("entradas.cliente", "Cliente", EntradasCategoryName, RequiresTabAccountAndClient: true),
        new("entradas.metodo-dinheiro", CashRepaymentMethodName, EntradasCategoryName,
            RequiresTabAccountAndClient: true, FiadoPaymentKind: FiadoPaymentKind.CountedCash),
        new("entradas.metodo-pix", PixRepaymentMethodName, EntradasCategoryName,
            RequiresTabAccountAndClient: true, FiadoPaymentKind: FiadoPaymentKind.NonCash),
        new("saidas.pgto-premio", "Pgto Prêmio", SaidasCategoryName),
        new("comerciais.desconto", "Desconto", "Despesas Comerciais"),
        new("comerciais.volante-rejeitado", "Volante rejeitado", "Despesas Comerciais"),
        new("comerciais.tarifa-cartao", "Tarifa cartão", "Despesas Comerciais"),
        new("comerciais.outras-despesas", "Outras Despesas", "Despesas Comerciais"),
        .. ExpenseSeeds.Select(seed => new TypeSeed(seed.Key, seed.Name, seed.CategoryName)),
        new("sistema.fiado-venda", FiadoSaleCounterpartTypeName, FiadoAutomaticOutCategoryName,
            SystemRole: TransactionTypeSystemRole.FiadoSaleCounterpart),
        new("sistema.fiado-recebimento-dinheiro", FiadoCashRepaymentCounterpartTypeName, FiadoAutomaticInCategoryName,
            SystemRole: TransactionTypeSystemRole.FiadoCashRepaymentCounterpart),
        new("sistema.cofre-deposito", SafeDepositInTypeName, SafeAutomaticInCategoryName,
            SystemRole: TransactionTypeSystemRole.SafeDepositIn),
        new("sistema.fiado-conversao-cheque", ChequeFiadoConversionTypeName, FiadoAutomaticOutCategoryName,
            RequiresTabAccountAndClient: true, SystemRole: TransactionTypeSystemRole.ChequeFiadoConversion)
    ];

    private static readonly (string Name, int DisplayOrder)[] ProductSeeds =
    [
        ("Telesena", 1),
        ("Raspadinha", 2),
        ("Jogos", 3),
        ("Loteria Especial", 4),
        ("Dinheiro", 5),
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
                SeedKey = seed.Key,
                CategoryId = categoriesByName[seed.CategoryName].Id,
                SettlementRule = seed.SettlementRule,
                RequiresTabAccountAndClient = seed.RequiresTabAccountAndClient,
                SystemRole = seed.SystemRole,
                FiadoPaymentKind = seed.FiadoPaymentKind,
                SettlementInstrument = seed.SettlementInstrument,
                MonthlyFinancialRole = seed.MonthlyFinancialRole,
                FinancialClass = seed.SystemRole is { } role
                    ? FinancialRoleRules.FinancialClassOf(role)
                    : FinancialClass.Unclassified
            })
            .ToList();
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

    /// <summary>
    /// Initial effective-dated policy row for a new branch, mirroring the default Setting
    /// constants. Dated <see cref="DateTime.MinValue"/> so per-date resolution is total
    /// even for entries backdated before the branch's creation.
    /// </summary>
    public static TimeEntryPolicy CreateDefaultTimeEntryPolicy(Guid branchId)
    {
        return new TimeEntryPolicy
        {
            BranchId = branchId,
            EffectiveFrom = DateTime.MinValue,
            DailyTargetHours = DefaultDailyTargetHours,
            LunchDeductionOver6H = DefaultLunchDeductionOver6H,
            LunchDeductionOver4H = DefaultLunchDeductionOver4H
        };
    }
}
