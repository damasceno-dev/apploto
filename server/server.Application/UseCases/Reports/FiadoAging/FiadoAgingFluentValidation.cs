using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.FiadoAging;

internal class FiadoAgingFluentValidation : AbstractValidator<RequestFiadoAgingJson>
{
    public FiadoAgingFluentValidation()
    {
        When(r => r.AsOfDate.HasValue, () =>
        {
            RuleFor(r => r.AsOfDate!.Value)
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
        });

        RuleFor(r => r.Page).PageBounds();
        RuleFor(r => r.PageSize).PageSizeBounds();
    }
}
