using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 8: money due to arrive in <c>[AsOfDate, AsOfDate + 13]</c>, with overdue kept as its own scope — the same
/// computation as <c>GET /report/upcoming-settlements</c>. <c>NotConfigured</c> when no branch type carries an instrument.
/// </summary>
public class ResponseManagementOverviewUpcomingSettlementsJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime AsOfDate { get; init; }
    public DateTime DueDateFrom { get; init; }
    public DateTime DueDateTo { get; init; }
    public int TotalCount { get; init; }
    public decimal TotalValue { get; init; }
    public IReadOnlyList<ResponseSettlementInstrumentTotalJson> ByInstrument { get; init; } = [];
    public int OverdueCount { get; init; }
    public decimal OverdueValue { get; init; }
    public int PreviewLimit { get; init; }
    public IReadOnlyList<ResponseUpcomingSettlementItemJson> Items { get; init; } = [];
}
