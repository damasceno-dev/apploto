using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseHourBankEntryJson
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
    public HourBankEntryKind Kind { get; set; }
    public DateTime SourceDate { get; set; }
    public decimal SignedHours { get; set; }
    public decimal SignedContribution { get; set; }
    public Guid? PreviousId { get; set; }
    public bool Cancelled { get; set; }
    public string ExternalIdentity { get; set; } = string.Empty;
}
