using server.Domain.Entities.Enums;

namespace server.Domain.Models;

/// <summary>
/// Upcoming-settlement selection (M11 Phase 5): Active, unpaid rows whose type — active or retired — carries an
/// instrument other than <c>None</c>, due in <c>[DueDateFrom, DueDateTo]</c> (both inclusive; an omitted
/// <see cref="DueDateFrom"/> is unbounded, which is how the overdue scope is read).
/// </summary>
public record UpcomingSettlementFilter(
    DateTime? DueDateFrom,
    DateTime DueDateTo,
    Guid? AccountId,
    SettlementInstrument? Instrument);
