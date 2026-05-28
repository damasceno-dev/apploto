namespace server.Communication.Requests;

public class RequestDailyLedgerJson
{
    public Guid AccountId { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
