using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Reports;

internal static class ReportValidationExtensions
{
    internal const int PageMin = 1;
    internal const int PageSizeMin = 1;
    internal const int PageSizeMax = 200;
    internal const int DateRangeMaxDays = 366;

    extension<T>(IRuleBuilder<T, DateTime> rule)
    {
        public void DateRangeRequired()
        {
            rule
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
        }

        public void DateRangeNotInverted(Func<T, DateTime> otherSelector)
        {
            rule
                .Must((instance, dateFrom) => dateFrom <= otherSelector(instance))
                .WithMessage(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
        }

        public void DateRangeWithinCap(Func<T, DateTime> dateToSelector, int maxDays = DateRangeMaxDays)
        {
            rule
                .Must((instance, dateFrom) => (dateToSelector(instance) - dateFrom).TotalDays <= maxDays)
                .WithMessage(ResourcesErrorMessages.REPORT_DATE_RANGE_TOO_WIDE);
        }
    }

    extension<T>(IRuleBuilder<T, int> rule)
    {
        public void PageBounds()
        {
            rule
                .GreaterThanOrEqualTo(PageMin)
                .WithMessage(ResourcesErrorMessages.REPORT_PAGE_INVALID);
        }

        public void PageSizeBounds(int min = PageSizeMin, int max = PageSizeMax)
        {
            rule
                .InclusiveBetween(min, max)
                .WithMessage(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
        }
    }
}
