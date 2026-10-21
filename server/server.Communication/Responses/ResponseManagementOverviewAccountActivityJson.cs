using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 9: what moved on each account today under the daily-ledger predicates. Every active Terminal plus any
/// account, active or deactivated, with activity today; figures are per account only — there is no cross-account total.
/// Drill-down: <c>GET /report/daily-ledger?AccountId&amp;DateFrom=DateTo={Date}</c>.
/// </summary>
public class ResponseManagementOverviewAccountActivityJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime Date { get; init; }
    public int TotalCount { get; init; }
    public int PreviewLimit { get; init; }

    /// <summary>Ordered by account name, then account id.</summary>
    public IReadOnlyList<ResponseManagementOverviewAccountJson> Items { get; init; } = [];
}
