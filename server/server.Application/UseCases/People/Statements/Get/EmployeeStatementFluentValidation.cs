using FluentValidation;
using server.Application.UseCases.Reports;
using server.Communication.Requests;
using server.Exceptions;
namespace server.Application.UseCases.People;

public class EmployeeStatementFluentValidation : AbstractValidator<RequestEmployeeStatementJson>
{
    public EmployeeStatementFluentValidation()
    {
        RuleFor(r => r.OperatorId).NotEmpty().WithMessage(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        RuleFor(r => r.Year).InclusiveBetween(ReportValidationExtensions.YearMin, ReportValidationExtensions.YearMax).WithMessage(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE);
        RuleFor(r => r.Month).InclusiveBetween(1, 12).WithMessage(ResourcesErrorMessages.REPORT_MONTH_INVALID);
        RuleFor(r => r.Page).PageBounds();
        RuleFor(r => r.Page).PageOffsetWithinRange(r => r.PageSize);
        RuleFor(r => r.PageSize).PageSizeBounds();
        RuleFor(r => r.AsOfDate).Must(d => d == null || PeopleValidation.Calendar(d.Value)).WithMessage(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }
}
