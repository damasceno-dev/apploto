using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Domain.Interfaces;

public interface IAuthenticationService
{
    Task<User> GetAuthenticatedUser();
    Task<User> GetAuthorizedUser(Role requiredRole, params Role[] additionalRoles);
}