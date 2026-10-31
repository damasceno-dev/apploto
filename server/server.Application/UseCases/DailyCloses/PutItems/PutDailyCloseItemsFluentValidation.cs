using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses.PutItems;

public class PutDailyCloseItemsFluentValidation : AbstractValidator<RequestPutDailyCloseItemsJson>
{
    public PutDailyCloseItemsFluentValidation()
    {
        RuleFor(r => r.Items)
            .ValidateDailyCloseItems();

        When(r => r.Items is not null, () =>
        {
            RuleForEach(r => r.Items)
                .SetValidator(new DailyCloseItemFluentValidation());
        });

        RuleFor(r => r.Notes)
            .ValidateDailyCloseNotes();

        RuleFor(r => r.CloseFields)
            .Must(fields => fields is null || fields.GroupBy(field => field.Field).All(group => group.Count() == 1))
            .WithMessage(ResourcesErrorMessages.SESSION_CLOSE_FIELD_DUPLICATE);

        When(r => r.CloseFields is not null, () =>
        {
            RuleForEach(r => r.CloseFields)
                .NotNull()
                .WithMessage(ResourcesErrorMessages.SESSION_CLOSE_FIELD_INVALID)
                .SetValidator(new SessionCloseFieldFluentValidation());
        });
    }
}
