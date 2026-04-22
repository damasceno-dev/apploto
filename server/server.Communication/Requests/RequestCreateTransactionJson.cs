namespace server.Communication.Requests;

public class RequestCreateTransactionJson
{
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public string? Description { get; init; }
    public TimeOnly? TransactionTime { get; init; }
    public Guid TransactionTypeId { get; init; }
    public Guid AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime? DueDate { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public bool SaveAsDraft { get; init; }
}
