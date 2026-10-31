using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Transactions.SafeDeposit;

public class SafeDepositFluentValidation : AbstractValidator<RequestSafeDepositJson>
{
    public SafeDepositFluentValidation()
    {
        RuleFor(request => request.OpeningAcknowledgment!)
            .SetValidator(new Sessions.Start.SessionOpeningAcknowledgmentFluentValidation())
            .When(request => request.OpeningAcknowledgment is not null);
        RuleFor(request => request.HistoricalReason)
            .MaximumLength(Services.Sessions.SessionStarter.ReasonMaxLength)
            .WithMessage(ResourcesErrorMessages.SESSION_REASON_TOO_LONG);

        RuleFor(r => r.Date)
            .NotEqual(default(DateTime))
            .WithMessage(ResourcesErrorMessages.TRANSACTION_DATE_EMPTY);

        RuleFor(r => r.AccountId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_ACCOUNT_ID_EMPTY);

        RuleFor(r => r.SafeAccountId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.SAFE_DEPOSIT_DESTINATION_REQUIRED);

        RuleFor(r => r)
            .Must(r => r.AccountId == Guid.Empty || r.AccountId != r.SafeAccountId)
            .WithMessage(ResourcesErrorMessages.SAFE_DEPOSIT_SAME_ACCOUNT);

        RuleFor(r => r.Value)
            .ValueIsPositive()
            .ValuePrecisionWithin14x2();

        RuleFor(r => r.Description)
            .DescriptionMaxLength();

        RuleFor(r => r.RecordedByOperatorId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_RECORDED_BY_OPERATOR_ID_EMPTY)
            .When(r => r.RecordedByOperatorId.HasValue);
    }
}
