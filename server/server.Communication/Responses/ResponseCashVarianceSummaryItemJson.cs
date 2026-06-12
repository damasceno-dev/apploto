using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCashVarianceSummaryItemJson
{
    public DateTime Date { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public decimal VarianceValue { get; init; }
    public DailyCloseStatus DailyCloseStatus { get; init; }
}
