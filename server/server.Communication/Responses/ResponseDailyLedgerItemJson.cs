using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseDailyLedgerItemJson
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public Direction Direction { get; init; }
    public string? Description { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string RecordedByOperatorName { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
    public DateTime? PaidAt { get; init; }
}
