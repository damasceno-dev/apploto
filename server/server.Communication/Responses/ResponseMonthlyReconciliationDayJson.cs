namespace server.Communication.Responses;

/// <summary>One calendar day of the reconciliation month, with its account closes, transaction counts, and cash variance.</summary>
public class ResponseMonthlyReconciliationDayJson
{
    public DateTime Date { get; init; }

    /// <summary>Account closes recorded on this day (one per account), ordered by account name. Empty when none.</summary>
    public IReadOnlyList<ResponseMonthlyReconciliationDayCloseJson> Closes { get; init; } = [];

    public int ActiveTransactionCount { get; init; }
    public int DraftTransactionCount { get; init; }
    public int CancelledTransactionCount { get; init; }

    /// <summary>
    /// The day's total cash variance summed across all of its account closes (e.g. accounts of +10 and +5
    /// give +15). Cash variance is the "Diferença Caixa" counted-minus-expected cash difference. Zero when
    /// the day has no variance rows.
    /// </summary>
    public decimal NetVariance { get; init; }
}
