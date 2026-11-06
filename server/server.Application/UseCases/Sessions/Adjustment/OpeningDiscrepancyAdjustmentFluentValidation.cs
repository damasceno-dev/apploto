using FluentValidation;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.Sessions.Adjustment;

public class OpeningDiscrepancyAdjustmentFluentValidation : AbstractValidator<RequestOpeningDiscrepancyAdjustmentJson>
{
    public OpeningDiscrepancyAdjustmentFluentValidation()
    {
        RuleFor(r => r.DiscrepancyId).NotEmpty().WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_NOT_FOUND);
        RuleFor(r => r.ExpectedVersion).GreaterThan(0u).WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_STALE);
        RuleFor(r => r.ComparisonRevision).GreaterThan(0).WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_STALE);
        RuleFor(r => r.ExpectedAdjustmentSequence).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_STALE);
        RuleFor(r => r.Target).IsInEnum().WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_TARGET_INVALID).NotEqual(OpeningDiscrepancyAttribution.Unattributed)
            .WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_TARGET_INVALID);
        RuleFor(r => r.PostedOn).Must(d => d != default && d.TimeOfDay == TimeSpan.Zero)
            .WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Reason).RequiredReason();
    }
}
