using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.Transactions.Update;

public class UpdateTransactionFluentValidation : AbstractValidator<RequestUpdateTransactionJson>
{
    public UpdateTransactionFluentValidation()
    {
        RuleFor(r => r.Description)
            .DescriptionMaxLength();

        RuleFor(r => r.DueDate)
            .DueDateIsRequired();
    }
}
