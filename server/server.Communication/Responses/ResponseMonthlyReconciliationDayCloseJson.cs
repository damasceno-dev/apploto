using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>One account's close on a given day, with its own cash variance.</summary>
public class ResponseMonthlyReconciliationDayCloseJson
{
    public Guid DailyCloseId { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public DailyCloseStatus Status { get; init; }

    /// <summary>
    /// This account's own cash variance for the day — the "Diferença Caixa" (counted minus expected cash)
    /// value of its close. Distinct per account even when several accounts close on the same day.
    /// </summary>
    public decimal VarianceValue { get; init; }
}
