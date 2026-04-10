using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseBranchUserJson
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Role Role { get; set; }
}
