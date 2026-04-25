using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Transactions.Create;

public class CreateTransactionFluentValidation : AbstractValidator<RequestCreateTransactionJson>
{
    public CreateTransactionFluentValidation()
    {
        RuleFor(r => r.Date)
            .NotEqual(default(DateTime))
            .WithMessage(ResourcesErrorMessages.TRANSACTION_DATE_EMPTY);

        RuleFor(r => r.TransactionTypeId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_ID_EMPTY);

        RuleFor(r => r.AccountId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_ACCOUNT_ID_EMPTY);

        RuleFor(r => r.Value)
            .ValueIsPositive()
            .ValuePrecisionWithin14x2();

        RuleFor(r => r.Description)
            .DescriptionMaxLength();

        RuleFor(r => r.DueDate)
            .DueDateOnOrAfterDate(r => r.Date);

        RuleFor(r => r.RecordedByOperatorId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_RECORDED_BY_OPERATOR_ID_EMPTY)
            .When(r => r.RecordedByOperatorId.HasValue);
    }
}
