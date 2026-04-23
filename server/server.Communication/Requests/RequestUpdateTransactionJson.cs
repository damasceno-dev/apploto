namespace server.Communication.Requests;

public class RequestUpdateTransactionJson
{
    public string? Description { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? PaidAt { get; init; }
    public Guid? ClientId { get; init; }
    public TimeOnly? TransactionTime { get; init; }
}
