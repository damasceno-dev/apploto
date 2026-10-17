namespace server.Communication.Responses;

public class ResponseOperatorTransactionSummaryJson
{
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;

    /// <summary>False when a Manager/Admin drilled into a deactivated operator by id (M11 Phase 2.4).</summary>
    public bool OperatorActive { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public int TotalTransactionCount { get; init; }
    public decimal TotalInValue { get; init; }
    public decimal TotalOutValue { get; init; }
    public decimal NetValue { get; init; }

    /// <summary>
    /// The automatic share (M7.7 Phase 5.8): how many of the counted rows, and how much of the In/Out totals, are
    /// system-generated Terminal legs of fiado operations this operator initiated. A subset of the totals above,
    /// never an addition — a generated cash entrada is not a second receipt.
    /// </summary>
    public int GeneratedTransactionCount { get; init; }
    public decimal GeneratedInValue { get; init; }
    public decimal GeneratedOutValue { get; init; }
    public IReadOnlyList<ResponseOperatorCategoryTotalJson> ByCategory { get; init; } = [];
}
