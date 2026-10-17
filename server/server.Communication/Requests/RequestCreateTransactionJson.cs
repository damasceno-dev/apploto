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

    /// <summary>
    /// M7.7 Phase 5.6: true only on a fiado sale (Tab <c>Out</c> with a client) that hands the client counted stock;
    /// the server then also writes the generated Terminal <c>Out</c> on the Tab's paired terminal. Never assumed.
    /// </summary>
    public bool HandsOutCountedStock { get; init; }
}
