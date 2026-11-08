using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only evidenced people record; replacements link to the previous revision.</summary>
public class SpecialDrawAllocation : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid ActorUserId { get; init; }
    public DateTime PostedOn { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public uint Version { get; set; }
    public Guid DrawId { get; init; }
    public Guid SessionId { get; init; }
    public Guid SessionFieldEffectId { get; init; }
    public int SourceRevision { get; init; }
    public decimal SourceValue { get; init; }
    public DateTime SourceDate { get; init; }
    public decimal Amount { get; init; }
    public decimal SignedContribution { get; init; }
    public Guid? PreviousId { get; init; }
    public bool Cancelled { get; init; }
}
