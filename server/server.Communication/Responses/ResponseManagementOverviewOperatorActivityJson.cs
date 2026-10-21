using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 10: who recorded what today under the operator-summary predicates (recorded volume, not revenue; not close
/// ownership). Active operators plus any operator, active or deactivated, with rows today.
/// Drill-down: <c>GET /report/operator-summary?OperatorId&amp;DateFrom&amp;DateTo</c>.
/// </summary>
public class ResponseManagementOverviewOperatorActivityJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime Date { get; init; }
    public int TotalCount { get; init; }
    public int PreviewLimit { get; init; }

    /// <summary>Ordered by transaction count DESC, operator name, operator id.</summary>
    public IReadOnlyList<ResponseManagementOverviewOperatorJson> Items { get; init; } = [];
}
