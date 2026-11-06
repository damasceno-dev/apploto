namespace server.Communication.Responses;

/// <summary>Independent report windows; never a claim that a ledger or matched statement is the bank position.</summary>
public class ResponsePostedReportingContextJson
{
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime AsOfDate { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public bool BankPositionComplete { get; init; }
}
