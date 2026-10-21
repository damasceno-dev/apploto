using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

/// <summary>
/// <c>GET /report/upcoming-settlements</c> (M11 Phase 5): an inclusive due-date window of at most 366 days, optional
/// account and instrument filters, and a page.
/// </summary>
public class RequestUpcomingSettlementsJson
{
    public DateTime DueDateFrom { get; init; }
    public DateTime DueDateTo { get; init; }
    public Guid? AccountId { get; init; }

    /// <summary>Restricts every figure, the overdue ones included, to one instrument; <c>None</c> is not a filter.</summary>
    public SettlementInstrument? Instrument { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
