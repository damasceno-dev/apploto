using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestCommissionSourceJson : RequestPeopleEvidenceJson
{
    public Guid OperatorId { get; set; }
    public DateTime SourceDate { get; set; }
    public required decimal SignedAmount { get; set; }
    public string ExternalIdentity { get; set; } = string.Empty;
    public string SourceRole { get; set; } = string.Empty;
}
