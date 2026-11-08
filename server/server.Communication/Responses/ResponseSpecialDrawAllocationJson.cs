using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseSpecialDrawAllocationJson
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid BranchId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime PostedOn { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public uint Version { get; set; }
    public Guid DrawId { get; set; }
    public Guid SessionId { get; set; }
    public Guid SessionFieldEffectId { get; set; }
    public int SourceRevision { get; set; }
    public decimal SourceValue { get; set; }
    public DateTime SourceDate { get; set; }
    public decimal Amount { get; set; }
    public decimal SignedContribution { get; set; }
    public Guid? PreviousId { get; set; }
    public bool Cancelled { get; set; }
}
