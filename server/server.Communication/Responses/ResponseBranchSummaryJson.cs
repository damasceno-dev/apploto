using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseBranchSummaryJson
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cnpj { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public Role Role { get; set; }
}
