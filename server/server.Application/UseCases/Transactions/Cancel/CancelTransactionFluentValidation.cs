using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Transactions.Cancel;

public class CancelTransactionFluentValidation : AbstractValidator<RequestCancelTransactionJson>
{
    internal const int CancellationReasonMaxLength = 500;

    public CancelTransactionFluentValidation()
    {
        RuleFor(r => r.CancellationReason)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_CANCELLATION_REASON_EMPTY);

        RuleFor(r => r.CancellationReason)
            .MaximumLength(CancellationReasonMaxLength)
            .WithMessage(string.Format(
                ResourcesErrorMessages.TRANSACTION_CANCELLATION_REASON_MAX_LENGTH,
                CancellationReasonMaxLength));
    }
}
