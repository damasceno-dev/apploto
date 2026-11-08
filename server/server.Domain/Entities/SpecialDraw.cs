using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only evidenced people record; replacements link to the previous revision.</summary>
public class SpecialDraw : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid ActorUserId { get; init; }
    public DateTime PostedOn { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public uint Version { get; set; }
    public string Name { get; init; } = string.Empty;
    public string Modality { get; init; } = string.Empty;
    public string Contest { get; init; } = string.Empty;
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public decimal Prize { get; init; }
    public bool Enabled { get; init; } = true;
    public Guid? PreviousId { get; init; }
}
