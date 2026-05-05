using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Holidays.List;

public class ListHolidaysFluentValidation : AbstractValidator<RequestListHolidaysJson>
{
    internal const int MaximumPageSize = 100;

    public ListHolidaysFluentValidation()
    {
        RuleFor(r => r.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(ResourcesErrorMessages.HOLIDAY_LIST_PAGE_INVALID);

        RuleFor(r => r.PageSize)
            .InclusiveBetween(1, MaximumPageSize)
            .WithMessage(string.Format(ResourcesErrorMessages.HOLIDAY_LIST_PAGE_SIZE_INVALID, 1, MaximumPageSize));

        RuleFor(r => r)
            .Must(r => r.DateFrom is null || r.DateTo is null || r.DateFrom.Value <= r.DateTo.Value)
            .WithMessage(ResourcesErrorMessages.HOLIDAY_LIST_DATE_RANGE_INVALID);
    }
}
