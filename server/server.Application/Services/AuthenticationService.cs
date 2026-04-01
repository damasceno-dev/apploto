using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services;

public class AuthenticationService(ITokenProvider tokenProvider, ITokenServices tokenServices, IUsersRepository usersRepository) : IAuthenticationService
{
    public async Task<User> GetAuthenticatedUser()
    {
        var cachedUser = tokenProvider.GetCachedUser();
        if (cachedUser is not null)
        {
            return cachedUser;
        }
        
        var token = tokenProvider.GetTokenValue();
        if (string.IsNullOrWhiteSpace(token))
            throw new TokenEmptyException(ResourcesErrorMessages.TOKEN_EMPTY);
        
        var result = tokenServices.ValidateToken(token);

        if (result.IsSuccess is false)
        {
            throw result.Error switch
            {
                TokenErrorType.Expired => new TokenExpiredException(ResourcesErrorMessages.TOKEN_EXPIRED),
                _ => new TokenInvalidException(ResourcesErrorMessages.TOKEN_INVALID)
            };
        }
        
        var user = await usersRepository.GetById(result.UserId);
        
        tokenProvider.CacheUser(user ?? throw new TokenWithoutUserException(ResourcesErrorMessages.TOKEN_WITHOUT_USER));
        
        return user;
    }

    public async Task<User> GetAuthorizedUser(Role requiredRole, params Role[] additionalRoles)
    {
        var user = await GetAuthenticatedUser();

        var roles = new List<Role> { requiredRole }.Union(additionalRoles).ToArray();
        return roles.Contains(user.Role) is false ? 
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION) : 
            user;
    }
}