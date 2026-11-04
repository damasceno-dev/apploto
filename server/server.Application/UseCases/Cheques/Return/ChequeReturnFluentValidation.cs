using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Cheques.Return;

public class ChequeReturnFluentValidation : AbstractValidator<RequestChequeReturnJson>
{
    public ChequeReturnFluentValidation()
    {
        RuleFor(r => r.DepositEventId).NotEmpty().WithMessage(ResourcesErrorMessages.CHEQUE_DEPOSIT_EVENT_REQUIRED);
        RuleFor(r => r.ExpectedVersion).ChequeExpectedVersion();
        RuleFor(r => r.OccurredOn).SettlementOccurredOn();
        RuleFor(r => r.PostedOn).SettlementPostedOn(r => r.OccurredOn);
        RuleFor(r => r.ClientId).NotEmpty().WithMessage(ResourcesErrorMessages.CHEQUE_CLIENT_REQUIRED);
        RuleFor(r => r.NextExpectedDepositDate)
            .Must((request, next) =>
                next.Date >= request.OccurredOn.Date &&
                next.Date <= request.OccurredOn.Date.AddDays(ChequeValidationExtensions.NextDepositMaxDays))
            .WithMessage(string.Format(
                ResourcesErrorMessages.CHEQUE_NEXT_DEPOSIT_DATE_INVALID, ChequeValidationExtensions.NextDepositMaxDays));
        RuleFor(r => r.ReturnReason)
            .MaximumLength(ChequeValidationExtensions.ReturnReasonMaxLength)
            .WithMessage(string.Format(
                ResourcesErrorMessages.CHEQUE_RETURN_REASON_MAX_LENGTH, ChequeValidationExtensions.ReturnReasonMaxLength));
    }
}
