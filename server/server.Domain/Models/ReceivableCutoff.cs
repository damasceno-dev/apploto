using server.Domain.Entities.Enums;

namespace server.Domain.Models;

/// <summary>
/// A receivable report's cutoff (M9.7 Phase 1.2): a row counts when its business date is on or before
/// <see cref="Date"/>, and an event counts when its <see cref="Basis"/> date (PostedOn by default, OccurredOn for
/// operational history) is on or before it.
/// </summary>
public sealed record ReceivableCutoff(ReceivableCutoffBasis Basis, DateTime Date);
