using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses.List;

public class ListDailyClosesFluentValidation : AbstractValidator<RequestListDailyClosesJson>
{
    internal const int MaximumPageSize = 100;

    public ListDailyClosesFluentValidation()
    {
        RuleFor(r => r.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_INVALID);

        RuleFor(r => r.PageSize)
            .InclusiveBetween(1, MaximumPageSize)
            .WithMessage(string.Format(ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_SIZE_INVALID, 1, MaximumPageSize));

        RuleFor(r => r.Status)
            .IsInEnum()
            .When(r => r.Status.HasValue)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_STATUS_INVALID);

        RuleFor(r => r)
            .Must(r => r.DateFrom is null || r.DateTo is null || r.DateFrom.Value <= r.DateTo.Value)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_LIST_DATE_RANGE_INVALID);

        RuleFor(r => r)
            .Must(r => r.Mine is false || r.OperatorId is null)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }
}
