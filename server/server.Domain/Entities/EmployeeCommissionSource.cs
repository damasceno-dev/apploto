using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only evidenced people record; replacements link to the previous revision.</summary>
public class EmployeeCommissionSource : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid ActorUserId { get; init; }
    public DateTime PostedOn { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public uint Version { get; set; }
    public Guid OperatorId { get; init; }
    public DateTime SourceDate { get; init; }
    public decimal SignedAmount { get; init; }
    public string ExternalIdentity { get; init; } = string.Empty;
    public string SourceRole { get; init; } = string.Empty;
    public Guid? PreviousId { get; init; }
    public decimal SignedContribution { get; init; }
}
