using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.GoLive.Openings;

public class GoLiveOpeningFluentValidation : AbstractValidator<RequestGoLiveOpeningJson>
{
    public GoLiveOpeningFluentValidation()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithMessage(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        RuleFor(x => x.TransactionTypeId).NotEmpty().WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        RuleFor(x => x.RecordedByOperatorId).NotEmpty().WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        RuleFor(x => x.AsOfDate).Must(x => x != default && x.TimeOfDay == TimeSpan.Zero).WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        RuleFor(x => x.SourceReference).NotEmpty().MaximumLength(100).WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        RuleFor(x => x.SourceEvidence).NotEmpty().MaximumLength(1000).WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        RuleFor(x => x.Items).NotNull().Must(x => x is { Count: > 0 and <= 120 }).WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        RuleForEach(x => x.Items).NotNull().WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID).ChildRules(item =>
        {
            item.RuleFor(x => x.Value).GreaterThan(0).LessThanOrEqualTo(999999999999.99m).Must(x => decimal.Round(x, 2) == x).WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
            item.RuleFor(x => x.DueDate).Must(x => x != default && x.TimeOfDay == TimeSpan.Zero).WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
        });
        RuleFor(x => x).Must(x => x.IsCheque || x.ClientId is not null && x.ClientId != Guid.Empty && x.Items is { Count: 1 }).WithMessage(ResourcesErrorMessages.GOLIVE_OPENING_INVALID);
    }
}
