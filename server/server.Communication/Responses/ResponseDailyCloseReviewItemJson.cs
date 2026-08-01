namespace server.Communication.Responses;

public class ResponseDailyCloseReviewItemJson
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public decimal? OpeningValue { get; init; }
    public decimal? ClosingValue { get; init; }
    public bool IsCashVarianceProduct { get; init; }
}
