using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Reports;

internal static class ReportValidationExtensions
{
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

        public void DateRangeWithinCap(Func<T, DateTime> dateToSelector, int maxDays = 366)
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
                .GreaterThanOrEqualTo(1)
                .WithMessage(ResourcesErrorMessages.REPORT_PAGE_INVALID);
        }

        public void PageSizeBounds(int min = 1, int max = 200)
        {
            rule
                .InclusiveBetween(min, max)
                .WithMessage(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
        }
    }
}
