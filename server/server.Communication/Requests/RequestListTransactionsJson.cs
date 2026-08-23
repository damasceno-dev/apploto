using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestListTransactionsJson
{
    public Guid? AccountId { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public TransactionStatus? Status { get; init; }
    public Guid? OperatorId { get; init; }
    public Guid? ClientId { get; init; }
    public Guid? OriginTransactionId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Convenience filter that resolves to the caller's linked operator id server-side.
    /// Cannot be combined with an explicit <see cref="OperatorId"/>; cannot change
    /// account scope or repository filtering semantics.
    /// </summary>
    public bool Mine { get; init; }
}
