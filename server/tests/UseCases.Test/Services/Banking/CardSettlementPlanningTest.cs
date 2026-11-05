using server.Application.UseCases.CardSettlements.Create;
using server.Application.UseCases.CardProviders.Create;
using server.Application.UseCases.BankStatements.AccountBinding;
using server.Application.UseCases.BankStatements.Import;
using server.Application.UseCases.BankStatements.Propose;
using server.Application.UseCases.BankStatements.Confirm;
using server.Application.UseCases.BankStatements.Get;
using server.Application.Services.Banking;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Application.Services.Receivables;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Banking;

public class CardSettlementPlanningTest
{
    [Theory]
    [InlineData(SettlementInstrument.CreditCard, false)]
    [InlineData(SettlementInstrument.DebitCard, false)]
    [InlineData(SettlementInstrument.Pix, false)]
    [InlineData(SettlementInstrument.MarketPlace, false)]
    [InlineData(SettlementInstrument.Cheque, true)]
    [InlineData(SettlementInstrument.None, true)]
    public void PlanIdentityRespectsExplicitPostedElectronicInstrument_AndPreservesLegacyUnclassifiedCheques(SettlementInstrument instrument, bool isCheque)
    {
        var row = new Transaction { OriginTransactionId = Guid.NewGuid(), PostedSettlementInstrument = instrument };
        ReceivableResponseBuilder.IsCheque(row, []).ShouldBe(isCheque);
    }

    [Fact]
    public void ProviderTimingMustBeExplicit_ZeroDelayIsAllowedOnlyWhenSupplied()
    {
        var validator = new CardProviderFluentValidation();
        Should.Throw<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<RequestCardProviderJson>("""{"Name":"Synthetic","ExternalIdentity":"synthetic","Brand":"Teste","BankAccountId":"00000000-0000-0000-0000-000000000001"}"""));
        var explicitZero = new RequestCardProviderJson { Name = "Synthetic", ExternalIdentity = "synthetic", Brand = "Teste", BankAccountId = Guid.NewGuid(), DelayDays = 0, BusinessDays = false };
        validator.Validate(explicitZero).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void NullDisplayedFingerprintIsAValidationFailureRatherThanAnException()
    {
        var request = new RequestBankMatchConfirmJson
        {
            PostedOn = new DateTime(2026, 10, 6),
            Items = [new() { ProposalId = Guid.NewGuid(), LineId = Guid.NewGuid(), LineVersion = 1, ReceiptId = Guid.NewGuid(), ReceiptVersion = 1, Amount = 100m, ExpectationFingerprint = null! }]
        };
        new BankMatchConfirmFluentValidation().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void OfxBindingRejectsControlCharactersBeforePersistence()
    {
        var request = new RequestBankStatementAccountBindingJson { BankAccountId = Guid.NewGuid(), BankIdentity = "104\0", AccountIdentity = "synthetic" };
        new BankStatementAccountBindingFluentValidation().Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ExplicitBusinessCalendarSkipsWeekendAndBranchHoliday_WithoutChangingCalendarProvider()
    {
        var friday = new DateTime(2026, 10, 9);
        var holidays = new HashSet<DateOnly> { new(2026, 10, 12) };
        CardSettlementUseCase.PlanArrival(friday, 2, true, holidays).ShouldBe(new DateTime(2026, 10, 14));
        CardSettlementUseCase.PlanArrival(friday, 2, false, holidays).ShouldBe(new DateTime(2026, 10, 11));
        CardSettlementUseCase.PlanArrival(friday, 0, true, holidays).ShouldBe(friday);
    }

    [Fact]
    public void AReturnedAllocationChangesTheDisplayedExpectationFingerprint_EvenIfReceiptVersionDoesNotChange()
    {
        var receipt = Guid.NewGuid(); var allocation = Guid.NewGuid();
        var before = new ResponseBankReceiptJson { Id = receipt, Version = 123, ExpectedAmount = 100, Allocations = [new() { SettlementEventId = allocation, Amount = 100 }] };
        var after = new ResponseBankReceiptJson { Id = receipt, Version = 123, ExpectedAmount = 0, Allocations = [new() { SettlementEventId = allocation, Amount = 100, ReturnedByEventId = Guid.NewGuid() }] };
        BankMatchService.Fingerprint(before).ShouldNotBe(BankMatchService.Fingerprint(after));
    }

    [Theory]
    [InlineData(null, false, true)]
    [InlineData(0.0, true, true)]
    [InlineData(1.0, true, true)]
    [InlineData(1.0, false, false)]
    [InlineData(-1.0, true, false)]
    [InlineData(0.001, true, false)]
    public void FeesRequireExplicitConfirmationAndEvidence_UnknownAndZeroAreDifferent(double? fee, bool confirmed, bool valid)
    {
        var request = new RequestCardSettlementJson
        {
            ProviderId = Guid.NewGuid(), ProviderVersion = 1, ExternalIdentity = "synthetic",
            OccurredOn = new DateTime(2026, 10, 1), PlanningBasisOn = new DateTime(2026, 9, 30),
            ConfirmedFee = fee is null ? null : (decimal)fee.Value, ManagerConfirmedFee = confirmed,
            FeeBasis = fee is null ? null : "Valor conferido", FeeEvidence = fee is null ? null : "Comprovante sintético",
            Items = [new() { TransactionId = Guid.NewGuid(), TransactionVersion = 1, GrossAmount = 100 }]
        };
        new CardSettlementFluentValidation().Validate(request).IsValid.ShouldBe(valid);
    }
    [Fact]
    public void ZeroBusinessDayDelayRollsWeekendAndHoliday_WhileCalendarZeroKeepsItsBasis()
    {
        var saturday = new DateTime(2026, 10, 3);
        var holidays = new HashSet<DateOnly> { new(2026, 10, 5) };
        CardSettlementUseCase.PlanArrival(saturday, 0, true, holidays).ShouldBe(new DateTime(2026, 10, 6));
        CardSettlementUseCase.PlanArrival(saturday, 0, false, holidays).ShouldBe(saturday);
    }

}
