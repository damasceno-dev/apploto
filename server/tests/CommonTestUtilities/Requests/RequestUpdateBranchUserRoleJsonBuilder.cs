using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestUpdateBranchUserRoleJsonBuilder
{
    private Role? _role = Role.Member;

    public RequestUpdateBranchUserRoleJsonBuilder WithRole(Role? role)
    {
        _role = role;
        return this;
    }

    public RequestUpdateBranchUserRoleJson Build()
    {
        return new RequestUpdateBranchUserRoleJson
        {
            Role = _role
        };
    }
}
