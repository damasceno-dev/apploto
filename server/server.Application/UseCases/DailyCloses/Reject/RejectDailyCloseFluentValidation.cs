using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses.Reject;

public class RejectDailyCloseFluentValidation : AbstractValidator<RequestRejectDailyCloseJson>
{
    internal const int RejectionReasonMaxLength = 500;

    public RejectDailyCloseFluentValidation()
    {
        RuleFor(request => request.RejectionReason)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_REJECTION_REASON_REQUIRED);

        RuleFor(request => request.RejectionReason)
            .MaximumLength(RejectionReasonMaxLength)
            .WithMessage(string.Format(
                ResourcesErrorMessages.DAILYCLOSE_REJECTION_REASON_LENGTH,
                RejectionReasonMaxLength));
    }
}
