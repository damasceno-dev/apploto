using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Authentication;

public class AuthenticationServiceTest
{
    [Fact]
    public async Task GetAuthenticatedUser_ShouldReturnCachedUser_WhenAvailable()
    {
        var cachedUser = new UserBuilder()
            .WithName("Cached User")
            .WithEmail("cached@example.com")
            .WithHashedPassword("Password123")
            .Build();

        var tokenProvider = new TokenProviderBuilder()
            .WithCachedUser(cachedUser)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();

        var service = CreateService(
            tokenProvider,
            tokenServices,
            new UsersRepositoryBuilder().Build(),
            new BranchUsersRepositoryBuilder().Build());

        var result = await service.GetAuthenticatedUser();

        result.ShouldBe(cachedUser);
        tokenServices.DidNotReceiveWithAnyArgs().ValidateToken(null!);
    }

    [Fact]
    public async Task GetAuthenticatedUser_ShouldThrowTokenEmptyException_WhenTokenIsMissing()
    {
        var tokenProvider = new TokenProviderBuilder()
            .WithCachedUser(null)
            .WithTokenValue(null)
            .Build();

        var service = CreateService(
            tokenProvider,
            new TokenServicesBuilder().Build(),
            new UsersRepositoryBuilder().Build(),
            new BranchUsersRepositoryBuilder().Build());

        await Should.ThrowAsync<TokenEmptyException>(service.GetAuthenticatedUser);
    }

    [Fact]
    public async Task GetAuthenticatedUser_ShouldThrowTokenExpiredException_WhenTokenIsExpired()
    {
        const string token = "expired-token";
        var tokenProvider = new TokenProviderBuilder()
            .WithCachedUser(null)
            .WithTokenValue(token)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .ValidateToken(token, new TokenResultValidation
            {
                IsSuccess = false,
                Error = TokenErrorType.Expired
            })
            .Build();

        var service = CreateService(
            tokenProvider,
            tokenServices,
            new UsersRepositoryBuilder().Build(),
            new BranchUsersRepositoryBuilder().Build());

        await Should.ThrowAsync<TokenExpiredException>(service.GetAuthenticatedUser);
    }

    [Fact]
    public async Task GetAuthenticatedUser_ShouldThrowTokenWithoutUserException_WhenTokenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        const string token = "global-token";
        var tokenProvider = new TokenProviderBuilder()
            .WithCachedUser(null)
            .WithTokenValue(token)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .ValidateToken(token, new TokenResultValidation
            {
                IsSuccess = true,
                UserId = userId,
                Scope = TokenScope.Global,
                Error = TokenErrorType.None
            })
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(null)
            .Build();

        var service = CreateService(
            tokenProvider,
            tokenServices,
            usersRepository,
            new BranchUsersRepositoryBuilder().Build());

        await Should.ThrowAsync<TokenWithoutUserException>(service.GetAuthenticatedUser);
    }

    [Fact]
    public async Task GetAuthorizedBranchUser_ShouldRejectGlobalToken()
    {
        const string token = "global-token";
        var tokenProvider = new TokenProviderBuilder()
            .WithCachedBranchUser(null)
            .WithTokenValue(token)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .ValidateToken(token, new TokenResultValidation
            {
                IsSuccess = true,
                UserId = Guid.NewGuid(),
                Scope = TokenScope.Global,
                Error = TokenErrorType.None
            })
            .Build();

        var service = CreateService(
            tokenProvider,
            tokenServices,
            new UsersRepositoryBuilder().Build(),
            new BranchUsersRepositoryBuilder().Build());

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => service.GetAuthorizedBranchUser(Role.Member));
    }

    [Fact]
    public async Task GetAuthorizedBranchUser_ShouldReturnBranchUser_WhenBranchTokenAndRoleAreValid()
    {
        var user = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithEmail("branch@example.com")
            .WithHashedPassword("Password123")
            .Build();
        var branchUser = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithUser(user)
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Manager)
            .Build();
        const string token = "branch-token";

        var tokenProvider = new TokenProviderBuilder()
            .WithCachedUser(null)
            .WithCachedBranchUser(null)
            .WithTokenValue(token)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .ValidateToken(token, new TokenResultValidation
            {
                IsSuccess = true,
                UserId = user.Id,
                BranchId = branchUser.BranchId,
                BranchUserId = branchUser.Id,
                Role = Role.Manager,
                Scope = TokenScope.Branch,
                Error = TokenErrorType.None
            })
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveById(branchUser.Id, branchUser)
            .Build();

        var service = CreateService(tokenProvider, tokenServices, usersRepository, branchUsersRepository);

        var result = await service.GetAuthorizedBranchUser(Role.Member, Role.Manager);

        result.ShouldBe(branchUser);
    }

    [Fact]
    public async Task GetAuthorizedBranchUser_ShouldThrowTokenInvalidException_WhenMembershipDoesNotMatchToken()
    {
        var user = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithEmail("branch@example.com")
            .WithHashedPassword("Password123")
            .Build();
        var branchUser = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithUser(user)
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Member)
            .Build();
        const string token = "branch-token";

        var tokenProvider = new TokenProviderBuilder()
            .WithCachedUser(null)
            .WithCachedBranchUser(null)
            .WithTokenValue(token)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .ValidateToken(token, new TokenResultValidation
            {
                IsSuccess = true,
                UserId = user.Id,
                BranchId = Guid.NewGuid(),
                BranchUserId = branchUser.Id,
                Role = Role.Member,
                Scope = TokenScope.Branch,
                Error = TokenErrorType.None
            })
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveById(branchUser.Id, branchUser)
            .Build();

        var service = CreateService(tokenProvider, tokenServices, usersRepository, branchUsersRepository);

        await Should.ThrowAsync<TokenInvalidException>(() => service.GetAuthorizedBranchUser(Role.Member));
    }

    private static AuthenticationService CreateService(
        ITokenProvider tokenProvider,
        ITokenServices tokenServices,
        IUsersRepository usersRepository,
        IBranchUsersRepository branchUsersRepository)
    {
        return new AuthenticationService(tokenProvider, tokenServices, usersRepository, branchUsersRepository);
    }
}
