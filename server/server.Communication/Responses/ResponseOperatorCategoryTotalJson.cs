namespace server.Communication.Responses;

public class ResponseOperatorCategoryTotalJson
{
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }

    /// <summary>The category's automatic share, already inside Count/TotalIn/TotalOut (M7.7 Phase 5.8).</summary>
    public int GeneratedCount { get; init; }
    public decimal GeneratedIn { get; init; }
    public decimal GeneratedOut { get; init; }
}
