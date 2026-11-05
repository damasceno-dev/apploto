using FluentValidation;
using server.Application.Services.Receivables;
using server.Application.UseCases.CardSettlements.Create;
using server.Application.UseCases.CardProviders.Create;
using server.Application.UseCases.Cheques;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BankMovements.Create;

public class BankMovementFluentValidation : AbstractValidator<RequestBankMovementJson>
{
    public BankMovementFluentValidation()
    {
        RuleFor(r => r.Kind).IsInEnum().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.BankAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.BankTransactionTypeId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.RecordedByOperatorId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Amount).Must(CardSettlementFluentValidation.ValidAmount).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.OccurredOn).InclusiveBetween(new DateTime(2000, 1, 1), new DateTime(2100, 12, 31)).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.Evidence).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(1000).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r).Must(r => r.Kind == BankMovementKind.SafeCollection ?
            r.SafeAccountId is not null && r.SafeAccountId != Guid.Empty && r.SafeAccountId != r.BankAccountId && r.SourceTransactionTypeId is not null && !r.ObservedBankMovement :
            r.SafeAccountId is null && r.SourceTransactionTypeId is null && r.ObservedBankMovement).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
    }
}

