namespace server.Communication.Responses;

/// <summary>
/// Money due to arrive (M11 Phase 5): Active, unpaid rows of a type — active or retired — classified with a
/// settlement instrument. Reads never mark anything paid, infer bank reconciliation or move money.
/// </summary>
public class ResponseUpcomingSettlementsJson
{
    /// <summary>Branch-local today; overdue means due before it.</summary>
    public DateTime AsOfDate { get; init; }

    /// <summary>
    /// False only when no branch type, active or retired, carries an instrument: nothing is classified, so the empty
    /// result means "not configured", never "nothing due".
    /// </summary>
    public bool IsConfigured { get; init; }

    public DateTime DueDateFrom { get; init; }
    public DateTime DueDateTo { get; init; }

    /// <summary>Whole-filter count and value of the rows due inside the window, independent of paging.</summary>
    public int TotalCount { get; init; }
    public decimal TotalValue { get; init; }

    /// <summary>Every classified instrument in declared order (zeros included) for the same window and filters.</summary>
    public IReadOnlyList<ResponseSettlementInstrumentTotalJson> ByInstrument { get; init; } = [];

    /// <summary>
    /// Rows due before <see cref="AsOfDate"/> under the same account/instrument filters, whatever the window. An
    /// independent figure: a window that starts before today also holds overdue rows, so the two are never added.
    /// </summary>
    public int OverdueCount { get; init; }
    public decimal OverdueValue { get; init; }

    public IReadOnlyList<ResponseUpcomingSettlementItemJson> Items { get; init; } = [];
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
}
