using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.TimeEntries.UpdateSegment;

public class UpdateTimeEntrySegmentFluentValidation : AbstractValidator<RequestUpdateTimeEntrySegmentJson>
{
    public UpdateTimeEntrySegmentFluentValidation()
    {
        RuleFor(r => r.ClockIn)
            .NotEqual(default(DateTime))
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED);

        // ClockOut is optional for an open segment; if supplied, it must create a positive duration.
        RuleFor(r => r)
            .Must(r => r.ClockOut is null || r.ClockOut > r.ClockIn)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
    }
}
