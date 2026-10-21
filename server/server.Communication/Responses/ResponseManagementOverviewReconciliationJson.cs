using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 4: the selected calendar month's lock context and readiness from the evaluator shared with
/// <c>POST /setting/lock-month</c> (advisory; the command rechecks). A month that has not elapsed is never lockable.
/// Drill-down: <c>GET /report/monthly-reconciliation/{Year}/{Month}</c>.
/// </summary>
public class ResponseManagementOverviewReconciliationJson
{
    public OverviewSectionStatus Status { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public DateTime? LockDate { get; init; }
    public bool IsMonthLocked { get; init; }
    public bool IsMonthElapsed { get; init; }
    public bool LockReady { get; init; }
    public int UnapprovedCloseBlockerCount { get; init; }
    public int DraftTransactionBlockerCount { get; init; }
    public int MissingExpectedCloseBlockerCount { get; init; }
}
