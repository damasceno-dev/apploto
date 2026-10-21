using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.MonthlyFinancial;

internal class MonthlyFinancialFluentValidation : AbstractValidator<RequestMonthlyFinancialReportJson>
{
    public MonthlyFinancialFluentValidation()
    {
        RuleFor(r => r.Year)
            .InclusiveBetween(ReportValidationExtensions.YearMin, ReportValidationExtensions.YearMax)
            .WithMessage(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE);

        RuleFor(r => r.Month)
            .InclusiveBetween(1, 12)
            .WithMessage(ResourcesErrorMessages.REPORT_MONTH_INVALID);
    }
}
