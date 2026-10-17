namespace server.Communication.Responses;

/// <summary>
/// Month-end reconciliation snapshot for a single branch: the gatekeeper view a Manager/Admin consults
/// before advancing <c>Setting.LockDate</c>.
/// </summary>
public class ResponseMonthlyReconciliationJson
{
    public int Year { get; init; }
    public int Month { get; init; }

    /// <summary>
    /// True only when every expected Terminal/date pair has an Approved close and there are no
    /// outstanding Draft transactions. Expected pairs come from a close in any state or direct
    /// active Terminal activity; no calendar or paired-Tab inference is applied.
    /// </summary>
    public bool LockReady { get; init; }

    /// <summary>
    /// The branch's current lock boundary (M11 Phase 2.3); null when the branch has never locked a month.
    /// </summary>
    public DateTime? LockDate { get; init; }

    /// <summary>True when the month's last day is on or before <see cref="LockDate"/>.</summary>
    public bool IsMonthLocked { get; init; }

    /// <summary>True when the month's last day is before the branch-local business date.</summary>
    public bool IsMonthElapsed { get; init; }

    /// <summary>One entry per calendar day of the month, including days with no closes and no transactions.</summary>
    public IReadOnlyList<ResponseMonthlyReconciliationDayJson> Days { get; init; } = [];

    /// <summary>Structured reasons the month is not lock-ready. Empty when <see cref="LockReady"/> is true.</summary>
    public IReadOnlyList<ResponseMonthlyReconciliationBlockerJson> Blockers { get; init; } = [];
}
