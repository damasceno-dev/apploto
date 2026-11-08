using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only evidenced people record; replacements link to the previous revision.</summary>
public class EmployeeCommissionRule : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid ActorUserId { get; init; }
    public DateTime PostedOn { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public uint Version { get; set; }
    public Guid OperatorId { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime EffectiveTo { get; init; }
    public decimal RatePercent { get; init; }
    public bool IncludeBolaoFee { get; init; }
    public bool IncludeSpecialFee { get; init; }
    public bool IncludeExplicitSources { get; init; }
    public bool Enabled { get; init; } = true;
    public Guid? PreviousId { get; init; }
}
