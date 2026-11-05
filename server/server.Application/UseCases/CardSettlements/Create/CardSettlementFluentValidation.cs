using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.CardSettlements.Create;

public class CardSettlementFluentValidation : AbstractValidator<RequestCardSettlementJson>
{
    public CardSettlementFluentValidation()
    {
        RuleFor(r => r.ProviderId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ProviderVersion).GreaterThan(0u).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ExternalIdentity).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(100).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.OccurredOn).InclusiveBetween(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31)).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.PlanningBasisOn).InclusiveBetween(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31)).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Items).NotNull().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).Must(items => items is { Count: > 0 and <= 200 } && items.All(i => i is not null && i.TransactionId != Guid.Empty && i.TransactionVersion > 0 && i.ExpectedVersion >= 0 && ValidAmount(i.GrossAmount)) && items.Select(i => i.TransactionId).Distinct().Count() == items.Count).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ConfirmedFee).Must(v => v is null || v >= 0 && v <= 999999999999.99m && decimal.Round(v.Value, 2) == v).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ObservedNetAmount).Must(v => v is null || ValidAmount(v.Value)).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.FeeBasis).MaximumLength(500).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.FeeEvidence).MaximumLength(1000).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r).Must(r => r.ConfirmedFee is null ? !r.ManagerConfirmedFee && string.IsNullOrWhiteSpace(r.FeeBasis) && string.IsNullOrWhiteSpace(r.FeeEvidence) : r.ManagerConfirmedFee && !string.IsNullOrWhiteSpace(r.FeeBasis) && !string.IsNullOrWhiteSpace(r.FeeEvidence)).WithMessage(ResourcesErrorMessages.CARD_FEE_EVIDENCE_REQUIRED);
    }

    internal static bool ValidAmount(decimal amount) => amount > 0 && amount <= 999999999999.99m && decimal.Round(amount, 2) == amount;
}

