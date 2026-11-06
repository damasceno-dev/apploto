using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.PersonalDifferences;

public class PersonalDifferencesFluentValidation : AbstractValidator<RequestPersonalDifferencesJson>
{
    public PersonalDifferencesFluentValidation()
    {
        RuleFor(r => r.Year).InclusiveBetween(ReportValidationExtensions.YearMin, ReportValidationExtensions.YearMax)
            .WithMessage(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE);
        RuleFor(r => r.Month).InclusiveBetween(1, 12).WithMessage(ResourcesErrorMessages.REPORT_MONTH_INVALID);
        RuleFor(r => r.Page).PageBounds();
        RuleFor(r => r.Page).PageOffsetWithinRange(r => r.PageSize);
        RuleFor(r => r.PageSize).PageSizeBounds();
        RuleFor(r => r.OperatorId).NotEqual(Guid.Empty).WithMessage(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        RuleFor(r => r.AsOfDate).Must(d => d == null || d.Value.TimeOfDay == TimeSpan.Zero)
            .WithMessage(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }
}
