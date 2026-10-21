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
        RuleFor(r => r.FiadoPaymentKind)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_FIADO_PAYMENT_KIND_ENUM_INVALID)
            .When(r => r.FiadoPaymentKind.HasValue);
        RuleFor(r => r.SettlementInstrument)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_INSTRUMENT_INVALID)
            .When(r => r.SettlementInstrument.HasValue);
        RuleFor(r => r.MonthlyFinancialRole)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_INVALID)
            .When(r => r.MonthlyFinancialRole.HasValue);
    }
}
