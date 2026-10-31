using server.Application.UseCases.Branches.Create;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.Create;

public class CreateBranchOpeningInflowsTest
{
    [Fact]
    public void CloseFieldTypes_ShouldBeSystemOwnedSameDayTypes_OutsideTheCountedProductSet()
    {
        // M9.6 Phase 7: Tarifa Bolão, Tarifa Especial and the signed Relatório do terminal (formerly Operador) are
        // written only by the close-form fields, so their types are system-owned and refused by the generic create.
        var branchId = Guid.NewGuid();
        var categories = CreateBranchSeedFactory.CreateDefaultCategories(branchId);
        var types = CreateBranchSeedFactory.CreateDefaultTransactionTypes(categories);
        var expected = new (string Name, string Category, Direction Direction, TransactionTypeSystemRole Role)[]
        {
            ("Tarifa Bolão", "Receita", Direction.In, TransactionTypeSystemRole.BolaoFee),
            ("Tarifa Especial", "Receita", Direction.In, TransactionTypeSystemRole.SpecialFee),
            ("Relatório do terminal", "Entradas", Direction.In, TransactionTypeSystemRole.TerminalReportIn),
            ("Relatório do terminal (negativo)", "Saídas", Direction.Out, TransactionTypeSystemRole.TerminalReportOut)
        };
        foreach (var (name, categoryName, direction, role) in expected)
        {
            var type = types.Single(t => t.Name == name);
            var category = categories.Single(c => c.Id == type.CategoryId);
            category.BranchId.ShouldBe(branchId);
            category.Name.ShouldBe(categoryName);
            category.DefaultDirection.ShouldBe(direction);
            type.SettlementRule.ShouldBe(SettlementRule.SameDay);
            type.RequiresTabAccountAndClient.ShouldBeFalse();
            type.SystemRole.ShouldBe(role);
            type.FiadoPaymentKind.ShouldBeNull();
            type.Active.ShouldBeTrue();
        }
        types.ShouldNotContain(t => t.Name == "Operador");

        var products = CreateBranchSeedFactory.CreateDefaultProducts(branchId);
        products.ShouldNotContain(p => p.Name == "Tarifa Bolão" || p.Name == "Tarifa Especial" || p.Name.StartsWith("Relatório"));
        products.ShouldAllBe(p => p.BranchId == branchId && p.Active);
        // The fields never remove physical cash or stock from the count.
        products.Select(p => p.Name).ShouldContain("Dinheiro");
        products.Select(p => p.Name).ShouldContain("Raspadinha");
    }
}
