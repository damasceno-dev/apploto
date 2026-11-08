using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponsePayItemJson
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid BranchId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime PostedOn { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public uint Version { get; set; }
    public Guid OperatorId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public DateTime SourceMonth { get; set; }
    public DateTime SourceDate { get; set; }
    public decimal Amount { get; set; }
    public PayItemKind Kind { get; set; }
    public string? DeductionPolicy { get; set; }
    public Guid? PreviousId { get; set; }
    public bool Cancelled { get; set; }
    public decimal SignedContribution { get; set; }
    public string? CommissionEvidenceJson { get; set; }
    public string? CommissionCalculationId { get; set; }
}
