using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only evidenced people record; replacements link to the previous revision.</summary>
public class EmployeePayItem : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid ActorUserId { get; init; }
    public DateTime PostedOn { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public uint Version { get; set; }
    public Guid OperatorId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public DateTime SourceMonth { get; init; }
    public DateTime SourceDate { get; init; }
    public decimal Amount { get; init; }
    public PayItemKind Kind { get; init; }
    public string? DeductionPolicy { get; init; }
    public Guid? PreviousId { get; init; }
    public bool Cancelled { get; init; }
    public decimal SignedContribution { get; init; }
    public string? CommissionEvidenceJson { get; init; }
    public string? CommissionCalculationId { get; init; }
}
