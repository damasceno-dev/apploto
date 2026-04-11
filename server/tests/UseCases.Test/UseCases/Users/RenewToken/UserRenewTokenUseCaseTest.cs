using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Users.RenewToken;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Users.RenewToken;

public class UserRenewTokenUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldIssueNewTokens_WhenRefreshTokenIsValid()
    {
        const string expectedToken = "new-global-token";
        const string expectedRefreshToken = "new-refresh-token";

        var user = new UserBuilder().Build();
        var existingRefreshToken = new RefreshToken
        {
            Value = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        var request = new RequestUserRenewTokenJsonBuilder()
            .WithValue(existingRefreshToken.Value)
            .Build();

        var refreshTokenRepository = new RefreshTokenRepositoryBuilder()
            .GetRefreshTokenEntity(existingRefreshToken)
            .Generate(expectedRefreshToken)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .GenerateGlobalToken(user, expectedToken)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(refreshTokenRepository, tokenServices, usersRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Token.ShouldBe(expectedToken);
        response.RefreshToken.ShouldBe(expectedRefreshToken);
        await refreshTokenRepository.Received(1).SaveRefreshToken(Arg.Is<RefreshToken>(token =>
            token.Value == expectedRefreshToken && token.UserId == user.Id));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowRefreshTokenException_WhenRefreshTokenIsMissing()
    {
        var request = new RequestUserRenewTokenJsonBuilder().Build();

        var refreshTokenRepository = new RefreshTokenRepositoryBuilder()
            .GetRefreshTokenEntity(null)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var usersRepository = new UsersRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(refreshTokenRepository, tokenServices, usersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<RefreshTokenException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.REFRESHTOKEN_INVALID);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowRefreshTokenException_WhenRefreshTokenIsExpired()
    {
        var existingRefreshToken = new RefreshToken
        {
            Value = Guid.NewGuid().ToString("N"),
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        var request = new RequestUserRenewTokenJsonBuilder()
            .WithValue(existingRefreshToken.Value)
            .Build();

        var refreshTokenRepository = new RefreshTokenRepositoryBuilder()
            .GetRefreshTokenEntity(existingRefreshToken)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var usersRepository = new UsersRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(refreshTokenRepository, tokenServices, usersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<RefreshTokenException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.REFRESHTOKEN_EXPIRED);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowRefreshTokenException_WhenUserIsMissing()
    {
        var existingRefreshToken = new RefreshToken
        {
            Value = Guid.NewGuid().ToString("N"),
            UserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var request = new RequestUserRenewTokenJsonBuilder()
            .WithValue(existingRefreshToken.Value)
            .Build();

        var refreshTokenRepository = new RefreshTokenRepositoryBuilder()
            .GetRefreshTokenEntity(existingRefreshToken)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(refreshTokenRepository, tokenServices, usersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<RefreshTokenException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.REFRESHTOKEN_WITHOUT_USER);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UserRenewTokenUseCase CreateUseCase(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenServices tokenServices,
        IUsersRepository usersRepository,
        IUnitOfWork unitOfWork)
    {
        return new UserRenewTokenUseCase(refreshTokenRepository, tokenServices, usersRepository, unitOfWork);
    }
}
