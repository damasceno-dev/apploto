namespace server.Communication.Responses;

public class ResponseFiadoBalanceJson
{
    public DateTime AsOfDate { get; init; }
    public IReadOnlyList<ResponseFiadoClientBalanceItemJson> Items { get; init; } = [];
    public decimal TotalOutstanding { get; init; }
}
