using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Reports.OpenChequeAging;

internal class OpenChequeAgingFluentValidation : AbstractValidator<RequestOpenChequeAgingJson>
{
    public OpenChequeAgingFluentValidation()
    {
        When(r => r.AsOfDate.HasValue, () =>
        {
            RuleFor(r => r.AsOfDate!.Value)
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
        });

        RuleFor(r => r.CutoffBasis)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.REPORT_CUTOFF_BASIS_INVALID)
            .When(r => r.CutoffBasis.HasValue);

        RuleFor(r => r.Page).PageBounds();
        RuleFor(r => r.PageSize).PageSizeBounds();
    }
}
