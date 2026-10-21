namespace server.Communication.Responses;

/// <summary>
/// <c>GET /report/monthly-financial/{year}/{month}</c> (M11 Phase 6, §7.2 step 4): official cash variance, the bolão
/// service fee and commercial leftovers per Terminal/day, and hours per operator/day, side by side. Distinct figures:
/// the report has no grand total across variance, fee, leftovers and hours. Not monthly reconciliation and not the
/// CEF borderô comparison, which stays manual.
/// </summary>
public class ResponseMonthlyFinancialReportJson
{
    public int Year { get; init; }
    public int Month { get; init; }

    /// <summary>Branch-local today and wall-clock time, read once; in-progress hours are measured at that instant.</summary>
    public DateTime AsOfDate { get; init; }
    public DateTime BranchLocalNow { get; init; }

    /// <summary>The month window: the calendar month, or for the current month its first day through today.</summary>
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public bool IsPartialMonth { get; init; }

    /// <summary>
    /// Persisted official <c>Diferença Caixa</c> of the window's non-Draft closes (Submitted, Approved, Rejected), each
    /// once; a Draft close's retained value is never read.
    /// </summary>
    public decimal OfficialVarianceTotal { get; init; }
    public int OfficialCloseCount { get; init; }

    /// <summary>True when any branch type, active or retired, is marked <c>BolaoServiceFee</c>.</summary>
    public bool IsBolaoServiceFeeConfigured { get; init; }

    /// <summary>Sum of the daily Terminal fee figures; <c>null</c> when not configured (never shown as zero).</summary>
    public decimal? BolaoServiceFeeTotal { get; init; }

    /// <summary>True when any branch type, active or retired, is marked <c>CommercialLeftover</c>.</summary>
    public bool IsCommercialLeftoverConfigured { get; init; }

    /// <summary>Sum of the leftover losses (positive = value lost); <c>null</c> when not configured.</summary>
    public decimal? CommercialLeftoverTotal { get; init; }
    public IReadOnlyList<ResponseMonthlyFinancialLeftoverTypeTotalJson> LeftoversByType { get; init; } = [];

    /// <summary>Payable hours of every operator in the window under each day's effective policy (§6.7).</summary>
    public decimal HoursTotal { get; init; }

    /// <summary>True when any listed day still has an open segment: today's is live and provisional.</summary>
    public bool ContainsInProgress { get; init; }

    /// <summary>One row per Terminal/day with a close or fee rows, ordered by date, account name, account id.</summary>
    public IReadOnlyList<ResponseMonthlyFinancialTerminalDayJson> TerminalDays { get; init; } = [];

    /// <summary>Leftover rows per date, Terminal, recording operator and type.</summary>
    public IReadOnlyList<ResponseMonthlyFinancialLeftoverJson> Leftovers { get; init; } = [];

    /// <summary>Hours per operator/day, ordered by date, operator name, operator id.</summary>
    public IReadOnlyList<ResponseMonthlyFinancialHoursDayJson> Hours { get; init; } = [];
}
