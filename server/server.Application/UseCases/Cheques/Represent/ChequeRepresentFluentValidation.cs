using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.Cheques.Represent;

public class ChequeRepresentFluentValidation : AbstractValidator<RequestChequeRepresentJson>
{
    public ChequeRepresentFluentValidation()
    {
        RuleFor(r => r.ExpectedVersion).ChequeExpectedVersion();
        RuleFor(r => r.OccurredOn).SettlementOccurredOn();
        RuleFor(r => r.PostedOn).SettlementPostedOn(r => r.OccurredOn);
    }
}
