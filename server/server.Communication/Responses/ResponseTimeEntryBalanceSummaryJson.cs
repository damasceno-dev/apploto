namespace server.Communication.Responses;

public class ResponseTimeEntryBalanceSummaryJson
{
    /// <summary>Manager/Admin only; attendance TotalBalanceHours retains its published period-only meaning.</summary>
    public List<ResponseHourBankJson>? HourBanks { get; init; }

    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public IReadOnlyList<ResponseTimeEntryBalanceOperatorJson> Operators { get; init; } = [];
}
