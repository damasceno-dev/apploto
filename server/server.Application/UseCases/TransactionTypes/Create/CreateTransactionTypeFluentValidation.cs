using FluentValidation;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.TransactionTypes.Create;

public class CreateTransactionTypeFluentValidation : AbstractValidator<RequestCreateTransactionTypeJson>
{
    public CreateTransactionTypeFluentValidation()
    {
        RuleFor(r => r.CategoryId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_CATEGORY_ID_EMPTY);
        RuleFor(r => r.Name).ValidateTransactionTypeName();
        RuleFor(r => r.SettlementRule)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_RULE_INVALID);
    }
}
