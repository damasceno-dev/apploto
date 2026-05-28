using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseFiadoAgingItemJson
{
    public Guid TransactionId { get; init; }
    public DateTime Date { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }
    public int DaysOutstanding { get; init; }
    public AgingBucket Bucket { get; init; }
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string? Description { get; init; }
}
