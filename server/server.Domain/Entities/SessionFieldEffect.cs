using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// The role slot of one close-form field in one session (M9.6 Phase 7, §3.30): at most one per session and field,
/// pointing at the field's current generated ledger effect (null while the value is zero). Every change appends a
/// <see cref="SessionFieldEffectRevision"/>; the slot itself never moves to another session or field.
/// </summary>
public class SessionFieldEffect : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid SessionId { get; init; }
    public DailyClose Session { get; init; } = null!;
    public SessionCloseField Field { get; init; }

    /// <summary>The field's signed value (only <see cref="SessionCloseField.TerminalReport"/> may be negative).</summary>
    public decimal Value { get; set; }

    /// <summary>Monotonic revision, equal to the latest history row's.</summary>
    public int Revision { get; set; }

    /// <summary>The Active generated row expressing <see cref="Value"/>; null when the value is zero.</summary>
    public Guid? CurrentTransactionId { get; set; }
    public Transaction? CurrentTransaction { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public ICollection<SessionFieldEffectRevision> Revisions { get; init; } = [];
}
