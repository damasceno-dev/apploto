using server.Application.UseCases.Branches.Create;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.Create;

/// <summary>M9.6 Phases 6, 7 and 9: the fresh-branch financial catalog, pinned by an independent literal list.</summary>
public class CreateBranchFinancialSeedTest
{
    // The 32 drawer expense types of the Access catalog (TblTipoCategoria 29–60), by category.
    private static readonly (string Category, string Name)[] ExpectedExpenses =
    [
        ("Despesas Administrativas", "Água"), ("Despesas Administrativas", "Aluguel"),
        ("Despesas Administrativas", "Carro Forte"), ("Despesas Administrativas", "Cel Fixo Internet"),
        ("Despesas Administrativas", "Contabilidade"), ("Despesas Administrativas", "Copa e Banheiro"),
        ("Despesas Administrativas", "Energia"), ("Despesas Administrativas", "Escritório"),
        ("Despesas Administrativas", "Jurídico"), ("Despesas Administrativas", "Marketing"),
        ("Despesas Administrativas", "Seguro TFL"), ("Despesas Administrativas", "Seguros"),
        ("Despesas Administrativas", "Sindicato"), ("Despesas Administrativas", "Segurança"),
        ("Despesas Administrativas", "Outras Despesas"),
        ("Despesas Pessoal", "13º salário"), ("Despesas Pessoal", "Comissão"), ("Despesas Pessoal", "Férias"),
        ("Despesas Pessoal", "FGTS"), ("Despesas Pessoal", "Horas extras"), ("Despesas Pessoal", "INSS"),
        ("Despesas Pessoal", "Pró Labore"), ("Despesas Pessoal", "Salário mensal"),
        ("Despesas Pessoal", "Vale Transporte"), ("Despesas Pessoal", "Vale Alimentação"),
        ("Despesas Pessoal", "Outras despesas"),
        ("Despesas Financeiras", "ISSQN"), ("Despesas Financeiras", "IPTU"),
        ("Despesas Financeiras", "Simples Nacional"), ("Despesas Financeiras", "Taxas Bancárias"),
        ("Despesas Financeiras", "Juros"), ("Despesas Financeiras", "Outras despesas")
    ];

    [Fact]
    public void Expenses_ShouldBeTheThirtyTwoAccessTypes_UnclassifiedOutTypes()
    {
        var (categories, types) = Seed();
        string CategoryOf(TransactionType type) => categories.Single(c => c.Id == type.CategoryId).Name;

        var expenseCategories = new[] { "Despesas Administrativas", "Despesas Pessoal", "Despesas Financeiras" };
        var seeded = types.Where(type => expenseCategories.Contains(CategoryOf(type))).ToList();

        seeded.Count.ShouldBe(32);
        seeded.Select(type => (CategoryOf(type), type.Name)).OrderBy(pair => pair).ShouldBe(ExpectedExpenses.OrderBy(pair => pair));
        seeded.ShouldAllBe(type =>
            type.SystemRole == null && type.FiadoPaymentKind == null && type.RequiresTabAccountAndClient == false &&
            type.SettlementRule == SettlementRule.SameDay && type.SettlementInstrument == SettlementInstrument.None &&
            type.MonthlyFinancialRole == MonthlyFinancialRole.None && type.SeedKey != null);
        categories.Where(c => expenseCategories.Contains(c.Name)).ShouldAllBe(c => c.DefaultDirection == Direction.Out);
        // The four Despesas Comerciais types were already seeded and stay as they were.
        types.Where(type => CategoryOf(type) == "Despesas Comerciais").Select(type => type.Name).Order().ShouldBe(
            ["Desconto", "Outras Despesas", "Sobra de Bolão", "Sobra de Federal", "Tarifa cartão", "Volante rejeitado"]);
    }

    [Fact]
    public void RepaymentMethods_ShouldBeDinheiroAndPix_WithNoDefault()
    {
        var (categories, types) = Seed();

        var methods = types.Where(type => type.FiadoPaymentKind != null).OrderBy(type => type.Name).ToList();

        methods.Select(type => (type.Name, type.FiadoPaymentKind)).ShouldBe([
            ("Dinheiro", (FiadoPaymentKind?)FiadoPaymentKind.CountedCash),
            ("PIX", FiadoPaymentKind.NonCash)
        ]);
        methods.ShouldAllBe(type => type.RequiresTabAccountAndClient && type.SystemRole == null);
        methods.ShouldAllBe(type => categories.Single(c => c.Id == type.CategoryId).Name == "Entradas");
        // Nothing in the catalog chooses a method: the old Entradas "Cliente" stays an unconfigured type.
        types.Single(type => type.Name == "Cliente" && categories.Single(c => c.Id == type.CategoryId).Name == "Entradas")
            .FiadoPaymentKind.ShouldBeNull();
    }

    [Fact]
    public void SystemTypes_ShouldCoverEveryRoleOnce_InTheirFixedCategories()
    {
        var (categories, types) = Seed();
        string CategoryOf(TransactionType type) => categories.Single(c => c.Id == type.CategoryId).Name;

        var system = types.Where(type => type.SystemRole != null).ToDictionary(type => type.SystemRole!.Value);

        system.Keys.Order().ShouldBe(Enum.GetValues<TransactionTypeSystemRole>().Order());
        CategoryOf(system[TransactionTypeSystemRole.FiadoSaleCounterpart]).ShouldBe("Movimentos automáticos de fiado (saída)");
        system[TransactionTypeSystemRole.FiadoSaleCounterpart].Name.ShouldBe("Saída de caixa — venda fiado");
        CategoryOf(system[TransactionTypeSystemRole.FiadoCashRepaymentCounterpart]).ShouldBe("Movimentos automáticos de fiado (entrada)");
        CategoryOf(system[TransactionTypeSystemRole.SafeDepositIn]).ShouldBe("Movimentos automáticos de cofre (entrada)");
        CategoryOf(system[TransactionTypeSystemRole.SafeDepositOut]).ShouldBe("Saídas");
        system[TransactionTypeSystemRole.SafeDepositOut].Name.ShouldBe("Depósito Dinheiro");
        system[TransactionTypeSystemRole.SafeDepositOut].SettlementRule.ShouldBe(SettlementRule.NextCalendarDay);
        // M9.7 Phase 2.3: the conversion debt is a Tab Out beside the counter-sale leg, never a payment method.
        CategoryOf(system[TransactionTypeSystemRole.ChequeFiadoConversion]).ShouldBe("Movimentos automáticos de fiado (saída)");
        system[TransactionTypeSystemRole.ChequeFiadoConversion].Name.ShouldBe("Cheque convertido em fiado");
        system[TransactionTypeSystemRole.ChequeFiadoConversion].RequiresTabAccountAndClient.ShouldBeTrue();
        // P6 classifies approved stable catalog identities; unresolved stock-event meanings stay explicit.
        types.Where(t => t.FinancialClass == FinancialClass.Unclassified).Select(t => t.SeedKey).Order()
            .ShouldBe(new[] { "saidas.encalhe-federal", "saidas.raspadinha", "saidas.telesena", "saidas.troca-telesena" });
        types.Where(t => t.SeedKey!.StartsWith("administrativas.") || t.SeedKey.StartsWith("pessoal.") || t.SeedKey.StartsWith("financeiras."))
            .ShouldAllBe(t => t.FinancialClass == FinancialClass.OperatingExpenseAndLoss);
        types.Where(t => t.SeedKey is "saidas.cartao-credito" or "saidas.cartao-debito" or "saidas.pix" or "saidas.marketplace" or "saidas.pgto-premio")
            .ShouldAllBe(t => t.FinancialClass == FinancialClass.CaixaAndClientMoney);
        types.Where(type => type.SeedKey is "saidas.recolhimento-cofre" or "entradas.recolhimento-banco")
            .ShouldAllBe(type => type.FinancialClass == FinancialClass.InternalTransfer && type.SystemRole == null);
        foreach (var (role, type) in system)
        {
            type.MonthlyFinancialRole.ShouldBe(FinancialRoleRules.MonthlyFinancialRoleOf(role));
            type.FiadoPaymentKind.ShouldBeNull();
            type.SettlementInstrument.ShouldBe(SettlementInstrument.None);
            var direction = categories.Single(c => c.Id == type.CategoryId).DefaultDirection;
            type.FinancialClass.ShouldBe(FinancialRoleRules.FinancialClassOf(role));
            direction.ShouldBe(role is TransactionTypeSystemRole.FiadoSaleCounterpart
                or TransactionTypeSystemRole.TerminalReportOut or TransactionTypeSystemRole.SafeDepositOut
                or TransactionTypeSystemRole.ChequeFiadoConversion
                ? Direction.Out
                : Direction.In);
        }
    }

    [Fact]
    public void Seeds_ShouldBeDeterministic_WithUniqueKeysPerCategory()
    {
        var (_, first) = Seed();
        var (_, second) = Seed();

        first.Select(type => (type.SeedKey, type.Name)).ShouldBe(second.Select(type => (type.SeedKey, type.Name)));
        first.ShouldAllBe(type => type.SeedKey != null);
        first.Select(type => type.SeedKey).Distinct().Count().ShouldBe(first.Count);
        first.GroupBy(type => (type.CategoryId, type.Name)).ShouldAllBe(group => group.Count() == 1);
    }

    private static (IReadOnlyList<Category> Categories, IReadOnlyList<TransactionType> Types) Seed()
    {
        var categories = CreateBranchSeedFactory.CreateDefaultCategories(Guid.NewGuid());
        return (categories, CreateBranchSeedFactory.CreateDefaultTransactionTypes(categories));
    }
}
