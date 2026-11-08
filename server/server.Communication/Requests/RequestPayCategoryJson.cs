using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestPayCategoryJson : RequestPeopleEvidenceJson
{
    public string Name { get; set; } = string.Empty;
    public PayItemKind Kind { get; set; }
    public bool Enabled { get; set; } = true;
}
