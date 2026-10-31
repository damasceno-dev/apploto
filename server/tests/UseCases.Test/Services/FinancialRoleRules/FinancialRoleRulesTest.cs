using server.Domain.Entities.Enums;
using server.Domain.Models;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.FinancialRoleRules;

/// <summary>M9.6 Phases 6, 7 and 9: the fixed relations between debt origins, close fields, roles and kinds.</summary>
public class FinancialRoleRulesTest
{
    [Theory]
    [InlineData(FiadoDebtOrigin.CounterSale, true)]
    [InlineData(FiadoDebtOrigin.OpeningObligation, false)]
    [InlineData(FiadoDebtOrigin.ImportedDebt, false)]
    [InlineData(FiadoDebtOrigin.ChequeConversion, false)]
    public void OnlyACounterSale_ShouldWriteACashboxLeg(FiadoDebtOrigin origin, bool expected)
    {
        server.Domain.Models.FinancialRoleRules.RequiresCashboxLeg(origin).ShouldBe(expected);
    }

    [Fact]
    public void EveryDebtOrigin_ShouldBeClassified()
    {
        Enum.GetValues<FiadoDebtOrigin>().Count(server.Domain.Models.FinancialRoleRules.RequiresCashboxLeg).ShouldBe(1);
    }

    [Theory]
    [InlineData(SessionCloseField.TerminalReport, 120.50, TransactionTypeSystemRole.TerminalReportIn)]
    [InlineData(SessionCloseField.TerminalReport, -0.01, TransactionTypeSystemRole.TerminalReportOut)]
    [InlineData(SessionCloseField.BolaoFee, 35, TransactionTypeSystemRole.BolaoFee)]
    [InlineData(SessionCloseField.SpecialFee, 15, TransactionTypeSystemRole.SpecialFee)]
    public void FieldValues_ShouldMapToTheirSystemRole_BySign(SessionCloseField field, double value, TransactionTypeSystemRole role)
    {
        server.Domain.Models.FinancialRoleRules.SystemRoleOf(field, (decimal)value).ShouldBe(role);
        server.Domain.Models.FinancialRoleRules.IsSessionFieldRole(role).ShouldBeTrue();
        server.Domain.Models.FinancialRoleRules.HasSourceTransaction(
            server.Domain.Models.FinancialRoleRules.GenerationKindOf(field)).ShouldBeFalse();
    }

    [Fact]
    public void GenerationKinds_ShouldSplitIntoSourceLegsAndFieldEffects()
    {
        var withSource = Enum.GetValues<TransactionGenerationKind>().Where(server.Domain.Models.FinancialRoleRules.HasSourceTransaction);
        withSource.ShouldBe([TransactionGenerationKind.FiadoSale, TransactionGenerationKind.FiadoCashRepayment, TransactionGenerationKind.SafeCashDeposit]);
        Enum.GetValues<SessionCloseField>().Select(server.Domain.Models.FinancialRoleRules.GenerationKindOf).ShouldBe([
            TransactionGenerationKind.SessionTerminalReport, TransactionGenerationKind.SessionBolaoFee, TransactionGenerationKind.SessionSpecialFee
        ]);
    }

    [Fact]
    public void OnlyTheAutomaticLegRoles_ShouldOwnACategory_AndOnlyFeesCarryAMonthlyRole()
    {
        Enum.GetValues<TransactionTypeSystemRole>().Where(server.Domain.Models.FinancialRoleRules.OwnsCategory).ShouldBe([
            TransactionTypeSystemRole.FiadoSaleCounterpart,
            TransactionTypeSystemRole.FiadoCashRepaymentCounterpart,
            TransactionTypeSystemRole.SafeDepositIn
        ]);
        Enum.GetValues<TransactionTypeSystemRole>()
            .Where(role => server.Domain.Models.FinancialRoleRules.MonthlyFinancialRoleOf(role) != MonthlyFinancialRole.None)
            .ShouldBe([TransactionTypeSystemRole.BolaoFee, TransactionTypeSystemRole.SpecialFee]);
    }
}
