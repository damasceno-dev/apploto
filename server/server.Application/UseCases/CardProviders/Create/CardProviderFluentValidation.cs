using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.CardProviders.Create;

public class CardProviderFluentValidation : AbstractValidator<RequestCardProviderJson>
{
    public CardProviderFluentValidation()
    {
        RuleFor(r => r.Instrument).IsInEnum().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).Must(i => i is server.Domain.Entities.Enums.SettlementInstrument.CreditCard or server.Domain.Entities.Enums.SettlementInstrument.DebitCard or server.Domain.Entities.Enums.SettlementInstrument.Pix or server.Domain.Entities.Enums.SettlementInstrument.MarketPlace).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Name).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(100).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ExternalIdentity).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(100).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Brand).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(50).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.BankAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.DelayDays).InclusiveBetween(0, 366).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
    }
}
