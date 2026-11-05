using server.Application.UseCases.CardSettlements.FeeEvidence;
using server.Communication.Requests;
using Shouldly;
using server.Exceptions;
using Xunit;

namespace Validators.Test.CardSettlements.FeeEvidence;

public class CardFeeEvidenceFluentValidationTest
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("zero", true)]
    [InlineData("max", true)]
    [InlineData("batch", false)]
    [InlineData("receipt", false)]
    [InlineData("version", false)]
    [InlineData("negative", false)]
    [InlineData("precision", false)]
    [InlineData("bound", false)]
    [InlineData("basis", false)]
    [InlineData("evidence", false)]
    [InlineData("reason", false)]
    [InlineData("long", false)]
    [InlineData("observed", false)]
    [InlineData("date", false)]
    public void ExplicitEvidenceMustBeCompleteAndExact(string scenario, bool valid)
    {
        var request = new RequestCardFeeEvidenceJson
        {
            BatchId = scenario == "batch" ? Guid.Empty : Guid.NewGuid(),
            ReceiptVersion = scenario == "receipt" ? 0u : 1u,
            ExpectedFeeEvidenceVersion = scenario == "version" ? -1 : 0,
            ConfirmedFee = scenario switch { "zero" => 0m, "max" => 999999999999.99m, "negative" => -1m, "precision" => 1.001m, "bound" => 1000000000000m, _ => 1m },
            ObservedNetAmount = scenario == "observed" ? -1m : 49m,
            Basis = scenario == "basis" ? " " : scenario == "long" ? new string('a', 501) : "Explicit basis",
            Evidence = scenario == "evidence" ? " " : "Synthetic evidence",
            Reason = scenario == "reason" ? " " : "Explicit reason",
            OccurredOn = scenario == "date" ? DateTime.MinValue : new DateTime(2026, 10, 6)
        };
        var result = new CardFeeEvidenceFluentValidation().Validate(request);
        result.IsValid.ShouldBe(valid);
        result.Errors.ShouldAllBe(e => e.ErrorMessage == ResourcesErrorMessages.BANKING_INPUT_INVALID || e.ErrorMessage == ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED);
    }
}
