using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestFinalizeInstallmentPlanJsonBuilder
{
    private List<RequestInstallmentPlanRowVersionJson>? _installments =
    [
        new RequestInstallmentPlanRowVersionJson { TransactionId = Guid.NewGuid(), Version = 11 },
        new RequestInstallmentPlanRowVersionJson { TransactionId = Guid.NewGuid(), Version = 12 }
    ];
    private bool _confirmMixedPlan;

    public RequestFinalizeInstallmentPlanJsonBuilder WithInstallments(List<RequestInstallmentPlanRowVersionJson>? installments)
    {
        _installments = installments;
        return this;
    }

    public RequestFinalizeInstallmentPlanJsonBuilder WithInstallment(Guid transactionId, uint version)
    {
        _installments ??= [];
        _installments.Add(new RequestInstallmentPlanRowVersionJson { TransactionId = transactionId, Version = version });
        return this;
    }

    public RequestFinalizeInstallmentPlanJsonBuilder WithConfirmMixedPlan(bool confirmMixedPlan)
    {
        _confirmMixedPlan = confirmMixedPlan;
        return this;
    }

    public RequestFinalizeInstallmentPlanJson Build()
    {
        return new RequestFinalizeInstallmentPlanJson
        {
            Installments = _installments,
            ConfirmMixedPlan = _confirmMixedPlan
        };
    }
}
