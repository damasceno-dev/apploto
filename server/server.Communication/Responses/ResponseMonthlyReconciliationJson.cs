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
    /// True only when every active close in the month is Approved AND there are no outstanding Draft
    /// transactions. When false, <see cref="Blockers"/> lists exactly why.
    /// </summary>
    public bool LockReady { get; init; }

    /// <summary>One entry per calendar day of the month, including days with no closes and no transactions.</summary>
    public IReadOnlyList<ResponseMonthlyReconciliationDayJson> Days { get; init; } = [];

    /// <summary>Structured reasons the month is not lock-ready. Empty when <see cref="LockReady"/> is true.</summary>
    public IReadOnlyList<ResponseMonthlyReconciliationBlockerJson> Blockers { get; init; } = [];
}
