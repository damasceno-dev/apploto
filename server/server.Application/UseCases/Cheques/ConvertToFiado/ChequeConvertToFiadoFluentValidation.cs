using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Cheques.ConvertToFiado;

public class ChequeConvertToFiadoFluentValidation : AbstractValidator<RequestChequeConvertToFiadoJson>
{
    public ChequeConvertToFiadoFluentValidation()
    {
        RuleFor(r => r.ExpectedVersion).ChequeExpectedVersion();
        RuleFor(r => r.ClientId).NotEmpty().WithMessage(ResourcesErrorMessages.CHEQUE_CLIENT_REQUIRED);
        RuleFor(r => r.TabAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.CHEQUE_FIADO_TAB_ACCOUNT_REQUIRED);
        RuleFor(r => r.OccurredOn).SettlementOccurredOn();
        RuleFor(r => r.PostedOn).SettlementPostedOn(r => r.OccurredOn);
        RuleFor(r => r.Description)
            .MaximumLength(ChequeValidationExtensions.DescriptionMaxLength)
            .WithMessage(string.Format(ResourcesErrorMessages.CHEQUE_DESCRIPTION_MAX_LENGTH, ChequeValidationExtensions.DescriptionMaxLength));
    }
}
