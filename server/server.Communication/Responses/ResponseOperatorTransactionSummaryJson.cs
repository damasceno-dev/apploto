namespace server.Communication.Responses;

public class ResponseOperatorTransactionSummaryJson
{
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public int TotalTransactionCount { get; init; }
    public decimal TotalInValue { get; init; }
    public decimal TotalOutValue { get; init; }
    public decimal NetValue { get; init; }
    public IReadOnlyList<ResponseOperatorCategoryTotalJson> ByCategory { get; init; } = [];
}
