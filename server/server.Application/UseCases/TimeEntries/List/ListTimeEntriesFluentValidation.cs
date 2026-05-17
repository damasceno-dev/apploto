using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.TimeEntries.List;

public class ListTimeEntriesFluentValidation : AbstractValidator<RequestListTimeEntriesJson>
{
    internal const int MaximumPageSize = 200;

    public ListTimeEntriesFluentValidation()
    {
        RuleFor(r => r.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_LIST_PAGE_INVALID);

        RuleFor(r => r.PageSize)
            .InclusiveBetween(1, MaximumPageSize)
            .WithMessage(string.Format(ResourcesErrorMessages.TIMEENTRY_LIST_PAGE_SIZE_INVALID, 1, MaximumPageSize));

        RuleFor(r => r)
            .Must(r => r.DateFrom is null || r.DateTo is null || r.DateFrom.Value <= r.DateTo.Value)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_LIST_DATE_RANGE_INVALID);

        RuleFor(r => r.Status)
            .IsInEnum()
            .When(r => r.Status.HasValue)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);

        RuleFor(r => r)
            .Must(r => r.Mine is not true || r.OperatorId is null)
            .WithMessage(ResourcesErrorMessages.TIMEENTRY_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }
}
