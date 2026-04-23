using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Transactions.CreateInstallment;

public class CreateTransactionInstallmentFluentValidation : AbstractValidator<RequestCreateTransactionInstallmentJson>
{
    internal const int MinimumInstallments = 2; // One cheque is handled by CreateTransactionUseCase
    internal const int MaximumInstallments = 24;

    public CreateTransactionInstallmentFluentValidation()
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

        RuleFor(r => r.DueDate)
            .DueDateOnOrAfterDate(r => r.Date);

        RuleFor(r => r.RecordedByOperatorId)
            .NotEqual(Guid.Empty)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_RECORDED_BY_OPERATOR_ID_EMPTY)
            .When(r => r.RecordedByOperatorId.HasValue);

        When(r => r.AutoGenerateInstallments, () =>
        {
            RuleFor(r => r.Installments)
                .Empty()
                .WithMessage(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_AUTO_GENERATE_CANNOT_HAVE_ITEMS);

            RuleFor(r => r.InstallmentCount)
                .NotNull()
                .WithMessage(string.Format(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_COUNT_OUT_OF_RANGE, MinimumInstallments, MaximumInstallments))
                .InclusiveBetween(MinimumInstallments, MaximumInstallments)
                .WithMessage(string.Format(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_COUNT_OUT_OF_RANGE, MinimumInstallments, MaximumInstallments));

            RuleFor(r => r.DueDate)
                .NotNull()
                .WithMessage(ResourcesErrorMessages.TRANSACTION_CHEQUE_REQUIRES_DUE_DATE);

            RuleFor(r => r.DueDate)
                .DueDateOnOrAfterDate(r => r.Date);

            RuleFor(r => r.DueDate)
                .Must(dueDate => dueDate is null || dueDate.Value.Date > DateTime.Today)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_FIRST_DUE_DATE_MUST_BE_FUTURE);
        });

        When(r => r.AutoGenerateInstallments is false, () =>
        {
            RuleFor(r => r.Installments)
                .Must(HasAllowedInstallmentCount)
                .WithMessage(string.Format(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_COUNT_OUT_OF_RANGE, MinimumInstallments, MaximumInstallments));

            RuleForEach(r => r.Installments)
                .ChildRules(item =>
                {
                    item.RuleFor(i => i.DueDate)
                        .NotEqual(default(DateTime))
                        .WithMessage(ResourcesErrorMessages.TRANSACTION_CHEQUE_REQUIRES_DUE_DATE);

                    item.RuleFor(i => i.Value)
                        .ValueIsPositive()
                        .ValuePrecisionWithin14x2();
                });

            RuleFor(r => r)
                .Must(InstallmentValuesMatchTotal)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_TOTAL_MISMATCH);

            RuleFor(r => r)
                .Must(DueDatesAreOnOrAfterTransactionDate)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);

            RuleFor(r => r)
                .Must(FirstDueDateIsInFuture)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_FIRST_DUE_DATE_MUST_BE_FUTURE);

            RuleFor(r => r)
                .Must(DueDatesAreStrictlyIncreasing)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_DUE_DATES_MUST_INCREASE);
        });
    }

    private static bool HasAllowedInstallmentCount(IReadOnlyList<RequestCreateTransactionInstallmentItemJson>? installments)
    {
        return installments is not null &&
               installments.Count is >= MinimumInstallments and <= MaximumInstallments;
    }

    private static bool InstallmentValuesMatchTotal(RequestCreateTransactionInstallmentJson request)
    {
        return HasAllowedInstallmentCount(request.Installments) &&
               request.Installments.Sum(installment => installment.Value) == request.Value;
    }

    private static bool DueDatesAreOnOrAfterTransactionDate(RequestCreateTransactionInstallmentJson request)
    {
        return HasAllowedInstallmentCount(request.Installments) &&
               request.Installments.All(installment => installment.DueDate >= request.Date);
    }

    private static bool FirstDueDateIsInFuture(RequestCreateTransactionInstallmentJson request)
    {
        return HasAllowedInstallmentCount(request.Installments) &&
               request.Installments[0].DueDate.Date > DateTime.Today;
    }

    private static bool DueDatesAreStrictlyIncreasing(RequestCreateTransactionInstallmentJson request)
    {
        if (HasAllowedInstallmentCount(request.Installments) is false)
        {
            return false;
        }

        for (var i = 1; i < request.Installments.Count; i++)
        {
            if (request.Installments[i].DueDate <= request.Installments[i - 1].DueDate)
            {
                return false;
            }
        }

        return true;
    }
}
