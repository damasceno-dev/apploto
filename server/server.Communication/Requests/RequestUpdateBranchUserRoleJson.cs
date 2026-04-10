using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestUpdateBranchUserRoleJson
{
    public Role? Role { get; init; }
}
