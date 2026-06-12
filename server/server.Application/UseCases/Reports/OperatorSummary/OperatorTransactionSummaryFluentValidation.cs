using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.OperatorSummary;

internal class OperatorTransactionSummaryFluentValidation : AbstractValidator<RequestOperatorTransactionSummaryJson>
{
    public OperatorTransactionSummaryFluentValidation()
    {
        RuleFor(r => r.DateFrom).DateRangeRequired();
        RuleFor(r => r.DateTo).DateRangeRequired();

        When(r => r.DateFrom != default && r.DateTo != default, () =>
        {
            RuleFor(r => r.DateFrom).DateRangeNotInverted(r => r.DateTo);
            RuleFor(r => r.DateFrom).DateRangeWithinCap(r => r.DateTo);
        });

        // No "OperatorId or Mine required" rule here: a linked Member may omit both
        // and fall back to their own operator (Phase 9.8). The Manager/Admin-only
        // requirement lives in the use case, where the role is known.
        RuleFor(r => r)
            .Must(r => r.Mine is false || r.OperatorId is null)
            .WithMessage(ResourcesErrorMessages.REPORT_MINE_AND_OPERATOR_ID_CONFLICT);
    }
}
