using FluentValidation;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.TimeEntries.Upsert;

public class UpsertTimeEntryFluentValidation : AbstractValidator<RequestUpsertTimeEntryJson>
{
    public UpsertTimeEntryFluentValidation()
    {
        RuleFor(r => r.OperatorId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_OPERATOR_ID_REQUIRED);

        RuleFor(r => r.Date)
            .NotEqual(default(DateTime))
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_DATE_REQUIRED);

        RuleFor(r => r.Status)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);

        RuleFor(r => r.Action)
            .IsInEnum()
            .When(r => r.Action.HasValue)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);

        RuleForEach(r => r.Segments)
            .ChildRules(segment =>
            {
                segment.RuleFor(s => s.ClockIn)
                    .NotEqual(default(DateTime))
                    .WithMessage(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED);

                segment.RuleFor(s => s)
                    .Must(s => s.ClockOut is null || s.ClockOut > s.ClockIn)
                    .WithMessage(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
            })
            .When(r => r.Segments is not null);
    }
}
