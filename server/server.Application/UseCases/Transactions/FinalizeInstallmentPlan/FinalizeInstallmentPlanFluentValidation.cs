using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Transactions.FinalizeInstallmentPlan;

internal class FinalizeInstallmentPlanFluentValidation : AbstractValidator<RequestFinalizeInstallmentPlanJson>
{
    public FinalizeInstallmentPlanFluentValidation()
    {
        RuleFor(request => request.Installments)
            .NotNull()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENTS_REQUIRED)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENTS_REQUIRED);

        When(request => request.Installments is { Count: > 0 }, () =>
        {
            // A null entry (e.g. "installments": [null]) is an installment without an id; every rule below
            // tolerates it so the request fails validation instead of dereferencing it.
            RuleFor(request => request.Installments!)
                .Must(installments =>
                    installments.All(installment => installment is not null && installment.TransactionId != Guid.Empty) &&
                    installments.Select(installment => installment!.TransactionId).Distinct().Count() == installments.Count)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_ID_INVALID);

            RuleFor(request => request.Installments!)
                .Must(installments => installments.All(installment => installment is null || installment.Version > 0))
                .WithMessage(ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_VERSION_INVALID);
        });
    }
}
