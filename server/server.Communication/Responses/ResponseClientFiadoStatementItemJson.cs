using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// One fiado movement of a client. <see cref="RunningBalance"/> is the client's signed balance right after this
/// movement over the full chronological history (<c>Date, CreatedAt, Id</c>), so consecutive movements reconcile on
/// any page.
/// </summary>
public class ResponseClientFiadoStatementItemJson
{
    public Guid TransactionId { get; init; }
    public DateTime Date { get; init; }
    public Direction Direction { get; init; }
    public decimal Value { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string TransactionTypeName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal RunningBalance { get; init; }
}
