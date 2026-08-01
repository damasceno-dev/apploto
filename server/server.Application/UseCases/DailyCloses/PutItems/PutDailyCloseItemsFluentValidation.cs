using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses.PutItems;

public class PutDailyCloseItemsFluentValidation : AbstractValidator<RequestPutDailyCloseItemsJson>
{
    public PutDailyCloseItemsFluentValidation()
    {
        RuleFor(r => r.Version)
            .NotEqual(0u)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_VERSION_REQUIRED);

        RuleFor(r => r.Items)
            .ValidateDailyCloseItems();

        When(r => r.Items is not null, () =>
        {
            RuleForEach(r => r.Items)
                .SetValidator(new DailyCloseItemFluentValidation());
        });

        RuleFor(r => r.Notes)
            .ValidateDailyCloseNotes();
    }
}
