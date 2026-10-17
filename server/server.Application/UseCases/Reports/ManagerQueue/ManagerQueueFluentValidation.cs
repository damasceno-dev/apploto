using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.ManagerQueue;

internal class ManagerQueueFluentValidation : AbstractValidator<RequestManagerQueueJson>
{
    public ManagerQueueFluentValidation()
    {
        RuleFor(r => r.Date)
            .NotEqual(default(DateTime))
            .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
