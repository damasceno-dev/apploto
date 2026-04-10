using server.Domain.Entities.Enums;

namespace server.Application.UseCases.BranchUsers;

internal static class BranchUserPermissionRules
{
    public static bool CanAssignRole(Role actorRole, Role targetRole)
    {
        return actorRole switch
        {
            Role.Admin => true,
            Role.Manager => targetRole is Role.Manager or Role.Member,
            _ => false
        };
    }
}
