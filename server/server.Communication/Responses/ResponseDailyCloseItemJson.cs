namespace server.Communication.Responses;

public class ResponseDailyCloseItemJson
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public DateTime CreatedAt { get; init; }
}
