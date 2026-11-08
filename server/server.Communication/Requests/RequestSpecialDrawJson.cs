using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestSpecialDrawJson : RequestPeopleEvidenceJson
{
    public string Name { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string Contest { get; set; } = string.Empty;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public required decimal Prize { get; set; }
    public bool Enabled { get; set; } = true;
}
