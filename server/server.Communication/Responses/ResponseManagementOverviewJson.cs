namespace server.Communication.Responses;

/// <summary>
/// The Dashboard's data (M11 Phase 7): a concise, read-only overview of the latest relevant fact from every manager
/// report, each section with its own window, status, whole-set totals and the ids its full report needs. One clock read
/// and one read-only snapshot back every section; any failure fails the whole response. It never replaces the Manager
/// Work Queue, and no figure here is added across sections.
/// </summary>
public class ResponseManagementOverviewJson
{
    /// <summary>The single clock read; every section is computed as of this instant.</summary>
    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>Branch-local today: point-in-time and day sections use it.</summary>
    public DateTime AsOfDate { get; init; }

    /// <summary>Branch-local wall time of <see cref="GeneratedAtUtc"/>; in-progress hours are measured at it.</summary>
    public DateTime BranchLocalNow { get; init; }

    public int Year { get; init; }
    public int Month { get; init; }

    /// <summary>The selected month's window: the calendar month, or its first day through today when current.</summary>
    public DateTime MonthDateFrom { get; init; }
    public DateTime MonthDateTo { get; init; }
    public bool IsPartialMonth { get; init; }

    public ResponseManagementOverviewQueueJson Queue { get; init; } = new();
    public ResponseManagementOverviewRecentClosesJson RecentCloses { get; init; } = new();
    public ResponseManagementOverviewCashVarianceJson CashVariance { get; init; } = new();
    public ResponseManagementOverviewReconciliationJson Reconciliation { get; init; } = new();
    public ResponseManagementOverviewFiadoJson Fiado { get; init; } = new();
    public ResponseManagementOverviewOpenChequesJson OpenCheques { get; init; } = new();
    public ResponseManagementOverviewUpcomingSettlementsJson UpcomingSettlements { get; init; } = new();
    public ResponseManagementOverviewAccountActivityJson AccountActivity { get; init; } = new();
    public ResponseManagementOverviewOperatorActivityJson OperatorActivity { get; init; } = new();
    public ResponseManagementOverviewHoursJson Hours { get; init; } = new();
    public ResponseManagementOverviewMonthlyFinancialJson MonthlyFinancial { get; init; } = new();
}
