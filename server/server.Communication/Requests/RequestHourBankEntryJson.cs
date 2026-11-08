using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestHourBankEntryJson : RequestPeopleEvidenceJson
{
    public Guid OperatorId { get; set; }
    public HourBankEntryKind Kind { get; set; }
    public DateTime SourceDate { get; set; }
    public required decimal SignedHours { get; set; }
    public bool Cancelled { get; set; }
    public string ExternalIdentity { get; set; } = string.Empty;
}
