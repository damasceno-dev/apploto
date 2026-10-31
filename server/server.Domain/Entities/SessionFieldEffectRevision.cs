using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// One immutable change of a close-form field (M9.6 Phase 7): the value before and after, the generated row created
/// and the one cancelled, who changed it and when. Zero keeps the history; nothing is ever deleted or rewritten.
/// </summary>
public class SessionFieldEffectRevision : EntityBase
{
    public Guid SessionFieldEffectId { get; init; }
    public SessionFieldEffect SessionFieldEffect { get; init; } = null!;
    public int Revision { get; init; }
    public decimal PreviousValue { get; init; }
    public decimal Value { get; init; }
    public Guid? CreatedTransactionId { get; init; }
    public Guid? CancelledTransactionId { get; init; }
    public SessionFieldEffectOrigin Origin { get; init; }
    public Guid? ActorUserId { get; init; }
    public DateTime RecordedAt { get; init; }
}
