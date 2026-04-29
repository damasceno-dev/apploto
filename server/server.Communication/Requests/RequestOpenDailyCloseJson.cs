namespace server.Communication.Requests;

public class RequestOpenDailyCloseJson
{
    public DateTime Date { get; init; }
    public Guid AccountId { get; init; }
}
