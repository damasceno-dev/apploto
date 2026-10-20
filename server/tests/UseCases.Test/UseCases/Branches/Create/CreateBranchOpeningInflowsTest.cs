using server.Application.UseCases.Branches.Create;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.Create;

public class CreateBranchOpeningInflowsTest
{
    [Fact]
    public void OpeningInflows_ShouldBeManualSameDayIncome_OutsideTheCountedProductSet()
    {
        var branchId = Guid.NewGuid();
        var categories = CreateBranchSeedFactory.CreateDefaultCategories(branchId);
        var types = CreateBranchSeedFactory.CreateDefaultTransactionTypes(categories);
        foreach (var name in new[] { "Tarifa Bolão", "Operador" })
        {
            var type = types.Single(t => t.Name == name);
            var category = categories.Single(c => c.Id == type.CategoryId);
            category.BranchId.ShouldBe(branchId);
            category.DefaultDirection.ShouldBe(Direction.In);
            category.Name.ShouldBe(name == "Tarifa Bolão" ? "Receita" : "Entradas");
            type.SettlementRule.ShouldBe(SettlementRule.SameDay);
            type.RequiresTabAccountAndClient.ShouldBeFalse();
            type.SystemRole.ShouldBeNull();
            type.FiadoPaymentKind.ShouldBeNull();
            type.Active.ShouldBeTrue();
        }
        var products = CreateBranchSeedFactory.CreateDefaultProducts(branchId);
        products.ShouldNotContain(p => p.Name == "Tarifa Bolão" || p.Name == "Operador");
        products.ShouldAllBe(p => p.BranchId == branchId && p.Active);
        // The correction must not remove physical cash or stock from the count.
        products.Select(p => p.Name).ShouldContain("Dinheiro");
        products.Select(p => p.Name).ShouldContain("Raspadinha");
    }
}
