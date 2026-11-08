using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestSpecialDrawAllocationJson : RequestPeopleEvidenceJson
{
    public Guid DrawId { get; set; }
    public Guid SessionId { get; set; }
    public Guid SessionFieldEffectId { get; set; }
    public int SourceRevision { get; set; }
    public DateTime SourceDate { get; set; }
    public decimal Amount { get; set; }
    public bool Cancelled { get; set; }
}
