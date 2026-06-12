namespace server.Communication.Responses;

public class ResponseCashVarianceSummaryJson
{
    public IReadOnlyList<ResponseCashVarianceSummaryItemJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
    public decimal TotalVariance { get; init; }
    public decimal MeanVariance { get; init; }
    public decimal MaxVariance { get; init; }
    public decimal MinVariance { get; init; }
}
