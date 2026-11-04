using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Cheques.Custody;

public class ChequeCustodyFluentValidation : AbstractValidator<RequestChequeCustodyJson>
{
    public ChequeCustodyFluentValidation()
    {
        RuleFor(r => r.SafeAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.SAFE_DEPOSIT_DESTINATION_REQUIRED);
        RuleFor(r => r.OccurredOn).SettlementOccurredOn();
        RuleFor(r => r.PostedOn).SettlementPostedOn(r => r.OccurredOn);
        RuleFor(r => r.Items).ChequeItems();
    }
}
