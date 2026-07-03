using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.Dashboard;

internal class DashboardFluentValidation : AbstractValidator<RequestDashboardJson>
{
    public DashboardFluentValidation()
    {
        RuleFor(r => r.Date)
            .NotEqual(default(DateTime))
            .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
