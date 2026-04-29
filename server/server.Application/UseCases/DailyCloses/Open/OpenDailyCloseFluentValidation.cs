using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses.Open;

public class OpenDailyCloseFluentValidation : AbstractValidator<RequestOpenDailyCloseJson>
{
    public OpenDailyCloseFluentValidation()
    {
        RuleFor(r => r.Date)
            .NotEqual(default(DateTime))
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_DATE_REQUIRED);

        RuleFor(r => r.AccountId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_REQUIRED);
    }
}
