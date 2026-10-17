using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// One cheque installment plan, identified by its origin transaction, with its computed status and
/// every installment (any status) ordered by due date. <see cref="TotalValue"/> excludes Cancelled rows.
/// </summary>
public class ResponseInstallmentPlanJson
{
    public Guid OriginTransactionId { get; init; }
    public InstallmentPlanStatus Status { get; init; }
    public Guid AccountId { get; init; }
    public DateTime Date { get; init; }
    public int DraftCount { get; init; }
    public int ActiveCount { get; init; }
    public int CancelledCount { get; init; }
    public decimal TotalValue { get; init; }
    public IReadOnlyList<ResponseTransactionJson> Installments { get; init; } = [];
}
