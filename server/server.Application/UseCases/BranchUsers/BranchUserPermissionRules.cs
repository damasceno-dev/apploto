using server.Domain.Entities.Enums;

namespace server.Application.UseCases.BranchUsers;

internal static class BranchUserPermissionRules
{
    public static bool CanManageRole(Role actorRole, Role targetCurrentRole, Role targetNextRole)
    {
        return CanManageMembership(actorRole, targetCurrentRole) && CanAssignRole(actorRole, targetNextRole);
    }

    public static bool CanManageMembership(Role actorRole, Role targetCurrentRole)
    {
        return actorRole switch
        {
            Role.Admin => true,
            Role.Manager => targetCurrentRole is Role.Manager or Role.Member,
            _ => false
        };
    }

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
