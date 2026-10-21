namespace server.Communication.Responses;

/// <summary>A marked leftover type's month total, so Bolão and Federal leftovers stay distinct.</summary>
public class ResponseMonthlyFinancialLeftoverTypeTotalJson
{
    public Guid TransactionTypeId { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public bool TransactionTypeActive { get; init; }
    public decimal Total { get; init; }
    public int RowCount { get; init; }
}
