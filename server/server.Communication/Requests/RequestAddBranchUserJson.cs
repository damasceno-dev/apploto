using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestAddBranchUserJson
{
    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public Role? Role { get; init; }
}
