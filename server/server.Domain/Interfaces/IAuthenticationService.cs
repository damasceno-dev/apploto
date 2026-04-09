using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Domain.Interfaces;

public interface IAuthenticationService
{
    Task<User> GetAuthenticatedUser();
    Task<BranchUser> GetAuthenticatedBranchUser();
    Task<BranchUser> GetAuthorizedBranchUser(Role requiredRole, params Role[] additionalRoles);
}
