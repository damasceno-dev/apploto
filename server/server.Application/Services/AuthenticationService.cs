using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services;

public class AuthenticationService(
    ITokenProvider tokenProvider,
    ITokenServices tokenServices,
    IUsersRepository usersRepository,
    IBranchUsersRepository branchUsersRepository) : IAuthenticationService
{
    public async Task<User> GetAuthenticatedUser()
    {
        var cachedUser = tokenProvider.GetCachedUser();
        if (cachedUser is not null)
        {
            return cachedUser;
        }

        var validationResult = ValidateToken();
        if (validationResult.Scope != TokenScope.Global)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        return await GetOrCacheUser(validationResult.UserId);
    }

    public async Task<BranchUser> GetAuthenticatedBranchUser()
    {
        var cachedBranchUser = tokenProvider.GetCachedBranchUser();
        if (cachedBranchUser is not null)
        {
            return cachedBranchUser;
        }

        var validationResult = ValidateToken();
        if (validationResult.Scope != TokenScope.Branch || validationResult.BranchUserId is null || validationResult.BranchId is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        var user = await GetOrCacheUser(validationResult.UserId);
        var branchUser = await branchUsersRepository.GetActiveById(validationResult.BranchUserId.Value);

        if (branchUser is null || branchUser.UserId != user.Id || branchUser.BranchId != validationResult.BranchId.Value)
        {
            throw new TokenInvalidException(ResourcesErrorMessages.TOKEN_INVALID);
        }

        tokenProvider.CacheBranchUser(branchUser);
        return branchUser;
    }

    public async Task<BranchUser> GetAuthorizedBranchUser(Role requiredRole, params Role[] additionalRoles)
    {
        var branchUser = await GetAuthenticatedBranchUser();

        var allowedRoles = new List<Role> { requiredRole }.Union(additionalRoles).ToArray();
        return allowedRoles.Contains(branchUser.Role)
            ? branchUser
            : throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    private TokenResultValidation ValidateToken()
    {
        var token = tokenProvider.GetTokenValue();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new TokenEmptyException(ResourcesErrorMessages.TOKEN_EMPTY);
        }

        var validationResult = tokenServices.ValidateToken(token);
        if (validationResult.IsSuccess)
        {
            return validationResult;
        }

        throw validationResult.Error switch
        {
            TokenErrorType.Expired => new TokenExpiredException(ResourcesErrorMessages.TOKEN_EXPIRED),
            _ => new TokenInvalidException(ResourcesErrorMessages.TOKEN_INVALID)
        };
    }

    private async Task<User> GetOrCacheUser(Guid userId)
    {
        var cachedUser = tokenProvider.GetCachedUser();
        if (cachedUser is not null)
        {
            return cachedUser;
        }

        var user = await usersRepository.GetById(userId) ??
                   throw new TokenWithoutUserException(ResourcesErrorMessages.TOKEN_WITHOUT_USER);

        tokenProvider.CacheUser(user);
        return user;
    }
}
