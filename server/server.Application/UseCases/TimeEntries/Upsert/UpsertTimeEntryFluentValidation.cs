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

        // Shape-only clock check: identical clocks would be a 0h shift, which is never a
        // valid record regardless of status (status-relative checks live in the use case).
        // Midnight-crossing pairs (e.g., 22:00 → 06:00) are intentionally accepted here and
        // resolved by TimeEntryCalculationService.
        RuleFor(r => r)
            .Must(r => r.ClockIn != r.ClockOut)
            .When(r => r.ClockIn.HasValue && r.ClockOut.HasValue)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_CLOCKS_EQUAL);
    }
}
