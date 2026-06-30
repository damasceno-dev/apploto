namespace server.Communication.Responses;

public class ResponseTimeEntryBalanceSummaryJson
{
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public IReadOnlyList<ResponseTimeEntryBalanceOperatorJson> Operators { get; init; } = [];
}
