namespace server.Communication.Requests;

public class RequestUpsertDailyCloseItemJson
{
    public Guid ProductId { get; init; }
    public decimal Value { get; init; }
}
