using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.MonthlyReconciliation;

internal class MonthlyReconciliationFluentValidation : AbstractValidator<RequestMonthlyReconciliationJson>
{
    public MonthlyReconciliationFluentValidation()
    {
        RuleFor(r => r.Year)
            .InclusiveBetween(2000, 2100)
            .WithMessage(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE);

        RuleFor(r => r.Month)
            .InclusiveBetween(1, 12)
            .WithMessage(ResourcesErrorMessages.REPORT_MONTH_INVALID);
    }
}
