namespace server.Communication.Responses;

/// <summary>Leftover loss recorded on a Terminal/day by one operator under one marked type (<c>Σ Out − Σ In</c>).</summary>
public class ResponseMonthlyFinancialLeftoverJson
{
    public DateTime Date { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public bool AccountActive { get; init; }
    public Guid RecordedByOperatorId { get; init; }
    public string RecordedByOperatorName { get; init; } = string.Empty;
    public bool RecordedByOperatorActive { get; init; }
    public Guid TransactionTypeId { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public int RowCount { get; init; }
}
