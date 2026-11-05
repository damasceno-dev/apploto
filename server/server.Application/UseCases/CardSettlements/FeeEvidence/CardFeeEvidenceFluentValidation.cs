using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.CardSettlements.FeeEvidence;

public class CardFeeEvidenceFluentValidation : AbstractValidator<RequestCardFeeEvidenceJson>
{
    public CardFeeEvidenceFluentValidation()
    {
        RuleFor(r => r.BatchId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ReceiptVersion).GreaterThan(0u).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ExpectedFeeEvidenceVersion).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ConfirmedFee).Must(v => v >= 0 && v <= 999999999999.99m && decimal.Round(v, 2) == v).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ObservedNetAmount).Must(v => v is null || Create.CardSettlementFluentValidation.ValidAmount(v.Value)).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Basis).NotEmpty().WithMessage(ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED).MaximumLength(500).WithMessage(ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED);
        RuleFor(r => r.Evidence).NotEmpty().WithMessage(ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED).MaximumLength(1000).WithMessage(ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED);
        RuleFor(r => r.Reason).NotEmpty().WithMessage(ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED).MaximumLength(500).WithMessage(ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED);
        RuleFor(r => r.OccurredOn).InclusiveBetween(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31)).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
    }
}
