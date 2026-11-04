using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Receivables.Get;

public class GetReceivableFluentValidation : AbstractValidator<RequestReceivableJson>
{
    public GetReceivableFluentValidation()
    {
        RuleFor(r => r.CutoffDate)
            .Must(date => date is null || date.Value != default)
            .WithMessage(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
        RuleFor(r => r.CutoffBasis)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.REPORT_CUTOFF_BASIS_INVALID)
            .When(r => r.CutoffBasis.HasValue);
    }
}
