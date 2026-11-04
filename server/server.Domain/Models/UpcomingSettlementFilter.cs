using server.Domain.Entities.Enums;

namespace server.Domain.Models;

/// <summary>
/// Upcoming-settlement selection (M11 Phase 5, M9.7 Phase 1): Active rows posted with an instrument other than
/// <c>None</c> (the frozen snapshot), still outstanding at the posted cutoff <see cref="AsOfDate"/>, due in
/// <c>[DueDateFrom, DueDateTo]</c> (both inclusive; an omitted <see cref="DueDateFrom"/> is unbounded, which is how
/// the overdue scope is read).
/// </summary>
public record UpcomingSettlementFilter(
    DateTime? DueDateFrom,
    DateTime DueDateTo,
    Guid? AccountId,
    SettlementInstrument? Instrument,
    DateTime AsOfDate);
