using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 11: hours in the selected month from the shared time-entry calculation (each day's effective policy, one
/// branch-local now). Positive and negative balances are summed apart. Live-now counts operators with an open segment
/// today; unresolved-open counts earlier operator-days still open (0 h until corrected). In-progress figures are
/// provisional. Drill-down: <c>GET /report/timeentry-balance?DateFrom&amp;DateTo</c>.
/// </summary>
public class ResponseManagementOverviewHoursJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public int TotalCount { get; init; }
    public int LiveNowCount { get; init; }
    public int UnresolvedOpenCount { get; init; }
    public bool ContainsInProgress { get; init; }
    public int PositiveBalanceCount { get; init; }
    public decimal PositiveBalanceHours { get; init; }
    public int NegativeBalanceCount { get; init; }

    /// <summary>Sum of the negative balances (zero or less).</summary>
    public decimal NegativeBalanceHours { get; init; }
    public int PreviewLimit { get; init; }

    /// <summary>Most behind first: balance ASC, operator name, operator id.</summary>
    public IReadOnlyList<ResponseManagementOverviewHoursOperatorJson> MostBehind { get; init; } = [];
}
