using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.ManagementOverview;

internal class ManagementOverviewFluentValidation : AbstractValidator<RequestManagementOverviewJson>
{
    public ManagementOverviewFluentValidation()
    {
        RuleFor(r => r)
            .Must(r => r.Year.HasValue == r.Month.HasValue)
            .WithMessage(ResourcesErrorMessages.REPORT_YEAR_MONTH_PAIR_REQUIRED);

        RuleFor(r => r.Year!.Value)
            .InclusiveBetween(ReportValidationExtensions.YearMin, ReportValidationExtensions.YearMax)
            .WithMessage(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE)
            .When(r => r.Year.HasValue);

        RuleFor(r => r.Month!.Value)
            .InclusiveBetween(1, 12)
            .WithMessage(ResourcesErrorMessages.REPORT_MONTH_INVALID)
            .When(r => r.Month.HasValue);
    }
}
