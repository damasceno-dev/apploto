using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.TimeEntryBalance;

internal class TimeEntryBalanceSummaryFluentValidation : AbstractValidator<RequestTimeEntryBalanceSummaryJson>
{
    public TimeEntryBalanceSummaryFluentValidation()
    {
        RuleFor(r => r.DateFrom).DateRangeRequired();
        RuleFor(r => r.DateTo).DateRangeRequired();

        When(r => r.DateFrom != default && r.DateTo != default, () =>
        {
            RuleFor(r => r.DateFrom).DateRangeNotInverted(r => r.DateTo);
            RuleFor(r => r.DateFrom).DateRangeWithinCap(r => r.DateTo);
        });

        // No "OperatorId or Mine required" rule here, by design: a linked Member omitting
        // both falls back to their own operator, and a Manager/Admin omitting both gets the
        // branch-wide roll-up (Phase 11 Addendum). The role-dependent routing lives in the
        // use case, where the caller's role is known.
        RuleFor(r => r)
            .Must(r => r.Mine is false || r.OperatorId is null)
            .WithMessage(ResourcesErrorMessages.REPORT_MINE_AND_OPERATOR_ID_CONFLICT);
    }
}
