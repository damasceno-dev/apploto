using FluentValidation;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.TransactionTypes.Update;

public class UpdateTransactionTypeFluentValidation : AbstractValidator<RequestUpdateTransactionTypeJson>
{
    public UpdateTransactionTypeFluentValidation()
    {
        RuleFor(r => r.Name).ValidateTransactionTypeName();
        RuleFor(r => r.SettlementRule)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_RULE_INVALID);
    }
}
