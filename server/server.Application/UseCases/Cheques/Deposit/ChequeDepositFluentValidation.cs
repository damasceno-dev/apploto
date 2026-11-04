using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Cheques.Deposit;

public class ChequeDepositFluentValidation : AbstractValidator<RequestChequeDepositJson>
{
    public ChequeDepositFluentValidation()
    {
        RuleFor(r => r.BankAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        RuleFor(r => r.OccurredOn).SettlementOccurredOn();
        RuleFor(r => r.PostedOn).SettlementPostedOn(r => r.OccurredOn);
        RuleFor(r => r.Description)
            .MaximumLength(ChequeValidationExtensions.DescriptionMaxLength)
            .WithMessage(string.Format(ResourcesErrorMessages.CHEQUE_DESCRIPTION_MAX_LENGTH, ChequeValidationExtensions.DescriptionMaxLength));
        RuleFor(r => r.Items).ChequeItems();
    }
}
