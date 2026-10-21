using server.Application.UseCases.Branches.Create;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.Create;

/// <summary>
/// M11 report classification on the fresh seed: only the working answers' named seed rows are classified; every other
/// seeded type — the system counterparts included — stays unclassified.
/// </summary>
public class CreateBranchReportClassificationSeedTest
{
    [Fact]
    public void SettlementInstrument_ShouldClassifyOnlyTheSeededCardAndChequeTypes()
    {
        var categories = CreateBranchSeedFactory.CreateDefaultCategories(Guid.NewGuid());
        var types = CreateBranchSeedFactory.CreateDefaultTransactionTypes(categories);

        var classified = types
            .Where(type => type.SettlementInstrument != SettlementInstrument.None)
            .ToDictionary(type => type.Name, type => type.SettlementInstrument);

        classified.ShouldBe(new Dictionary<string, SettlementInstrument>
        {
            ["Depósito Cheque"] = SettlementInstrument.Cheque,
            ["Cartão de Crédito"] = SettlementInstrument.CreditCard,
            ["Cartão de Débito"] = SettlementInstrument.DebitCard
        }, ignoreOrder: true);

        // Timing stays its own setting: the instrument never replaced the seeded settlement rules.
        types.Single(type => type.Name == "Depósito Cheque").SettlementRule.ShouldBe(SettlementRule.OperatorEnteredCheque);
        types.Single(type => type.Name == "Cartão de Crédito").SettlementRule.ShouldBe(SettlementRule.TwoBusinessDays);
        types.Single(type => type.Name == "Cartão de Débito").SettlementRule.ShouldBe(SettlementRule.NextBusinessDay);
        types.Where(type => type.SystemRole != null).ShouldAllBe(type => type.SettlementInstrument == SettlementInstrument.None);
    }

    [Fact]
    public void MonthlyFinancialRole_ShouldMarkOnlyTheSeededFeeAndLeftoverTypes_WithMatchingDirections()
    {
        var categories = CreateBranchSeedFactory.CreateDefaultCategories(Guid.NewGuid());
        var types = CreateBranchSeedFactory.CreateDefaultTransactionTypes(categories);

        var marked = types.Where(type => type.MonthlyFinancialRole != MonthlyFinancialRole.None).ToList();

        marked.Select(type => (type.Name, type.MonthlyFinancialRole)).OrderBy(pair => pair.Name).ShouldBe([
            ("Sobra de Bolão", MonthlyFinancialRole.CommercialLeftover),
            ("Sobra de Federal", MonthlyFinancialRole.CommercialLeftover),
            ("Tarifa Bolão", MonthlyFinancialRole.BolaoServiceFee)
        ]);
        foreach (var type in marked)
        {
            var direction = categories.Single(category => category.Id == type.CategoryId).DefaultDirection;
            direction.ShouldBe(type.MonthlyFinancialRole == MonthlyFinancialRole.BolaoServiceFee ? Direction.In : Direction.Out);
        }
        // Operador (question 9) is a separate inflow, never the fee.
        types.Single(type => type.Name == "Operador").MonthlyFinancialRole.ShouldBe(MonthlyFinancialRole.None);
        types.Where(type => type.SystemRole != null).ShouldAllBe(type => type.MonthlyFinancialRole == MonthlyFinancialRole.None);
    }
}
