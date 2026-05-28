using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.FiadoBalance;

internal class FiadoBalanceFluentValidation : AbstractValidator<RequestFiadoBalanceJson>
{
    public FiadoBalanceFluentValidation()
    {
        When(r => r.AsOfDate.HasValue, () =>
        {
            RuleFor(r => r.AsOfDate!.Value)
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
        });
    }
}
