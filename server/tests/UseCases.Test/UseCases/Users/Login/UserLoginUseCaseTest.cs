using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services;
using server.Application.UseCases.Users.Login;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Users.Login;

public class UserLoginUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnTokenResponse_WhenCredentialsAreValid()
    {
        const string expectedToken = "global-token";
        const string expectedRefreshToken = "refresh-token";
        const string plainPassword = "Password123";

        var user = new UserBuilder()
            .WithEmail("user@example.com")
            .WithHashedPassword(plainPassword)
            .Build();
        var request = new RequestUserLoginJsonBuilder()
            .WithEmail(user.Email)
            .WithPassword(plainPassword)
            .Build();

        var usersRepository = new UsersRepositoryBuilder()
            .GetByEmail(user)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .GenerateGlobalToken(user, expectedToken)
            .Build();
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder()
            .Generate(expectedRefreshToken)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(user.Name);
        response.Email.ShouldBe(user.Email);
        response.ResponseToken.Token.ShouldBe(expectedToken);
        response.ResponseToken.RefreshToken.ShouldBe(expectedRefreshToken);
        await refreshTokenRepository.Received(1).SaveRefreshToken(Arg.Is<RefreshToken>(token =>
            token.Value == expectedRefreshToken && token.UserId == user.Id));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowInvalidLogin_WhenEmailIsNotRegistered()
    {
        var request = new RequestUserLoginJsonBuilder().Build();

        var usersRepository = new UsersRepositoryBuilder()
            .GetByEmail(null)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        var exception = await Should.ThrowAsync<InvalidLoginException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.EMAIL_NOT_REGISTERED);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowInvalidLogin_WhenPasswordIsWrong()
    {
        var user = new UserBuilder()
            .WithEmail("user@example.com")
            .WithHashedPassword("Password123")
            .Build();
        var request = new RequestUserLoginJsonBuilder()
            .WithEmail(user.Email)
            .WithPassword("WrongPassword123")
            .Build();

        var usersRepository = new UsersRepositoryBuilder()
            .GetByEmail(user)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        var exception = await Should.ThrowAsync<InvalidLoginException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.PASSWORD_WRONG);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenRequestIsInvalid()
    {
        var request = new RequestUserLoginJsonBuilder()
            .WithEmail(string.Empty)
            .Build();

        var usersRepository = new UsersRepositoryBuilder().Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.EMAIL_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UserLoginUseCase CreateUseCase(
        IUsersRepository usersRepository,
        ITokenServices tokenServices,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        PasswordEncryption passwordEncryption)
    {
        return new UserLoginUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);
    }
}
