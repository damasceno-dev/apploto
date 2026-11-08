namespace server.Communication.Requests;

public class RequestPeopleEvidenceJson
{
    public DateTime PostedOn { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public Guid? PreviousId { get; set; }
}
