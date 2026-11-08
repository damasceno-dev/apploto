using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCommissionSourceJson
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
    public DateTime SourceDate { get; set; }
    public decimal SignedAmount { get; set; }
    public string ExternalIdentity { get; set; } = string.Empty;
    public string SourceRole { get; set; } = string.Empty;
    public Guid? PreviousId { get; set; }
    public decimal SignedContribution { get; set; }
}
