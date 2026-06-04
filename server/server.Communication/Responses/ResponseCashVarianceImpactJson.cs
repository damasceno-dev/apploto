using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCashVarianceImpactJson
{
    public Guid? AccountId { get; init; }
    public DateTime? Date { get; init; }

    /// <summary>
    /// Status of the `(branch, account, date)` daily close, or null when no close exists. Lets the
    /// caller tell what kind of variance number this is — pending (<c>Submitted</c>), signed-off
    /// (<c>Approved</c>), repudiated (<c>Rejected</c>), or still being counted (<c>Draft</c>) —
    /// since editing a transaction under each carries a different consequence.
    /// </summary>
    public DailyCloseStatus? DailyCloseStatus { get; init; }

    /// <summary>
    /// The day's cash variance (§6.12) as it stands now, live-recomputed. Populated whenever a
    /// complete closing-count set exists — any close status except <c>Draft</c>
    /// (<c>Submitted</c>/<c>Approved</c>/<c>Rejected</c> all retain their last-submitted counts).
    /// Null for a <c>Draft</c> close (counts still being entered) or an absent close.
    /// </summary>
    public decimal? CurrentVariance { get; init; }

    /// <summary>
    /// The variance the day would show after the edit (<c>CurrentVariance + VarianceDelta</c>).
    /// Non-null only when <see cref="CurrentVariance"/> is non-null (any non-<c>Draft</c> close).
    /// </summary>
    public decimal? ProjectedVariance { get; init; }

    /// <summary>
    /// Always the computed net-flow delta (before − after). Structurally 0 under today's edit
    /// contract because no editable field changes the §6.12 inputs, but computed, not assumed.
    /// </summary>
    public decimal VarianceDelta { get; init; }
}
