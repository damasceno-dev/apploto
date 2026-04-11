using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services;
using server.Application.UseCases.Users.Register;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Users.Register;

public class UserRegisterUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnTokenResponse_WhenRequestIsValid()
    {
        const string expectedToken = "global-token";
        const string expectedRefreshToken = "refresh-token";
        var request = new RequestUserRegisterJsonBuilder().Build();

        var usersRepository = new UsersRepositoryBuilder()
            .VerifyIfEmailAlreadyExists(false)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        tokenServices.GenerateGlobalToken(Arg.Any<User>()).Returns(expectedToken);
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder()
            .Generate(expectedRefreshToken)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(request.Name);
        response.Email.ShouldBe(request.Email);
        response.ResponseToken.Token.ShouldBe(expectedToken);
        response.ResponseToken.RefreshToken.ShouldBe(expectedRefreshToken);

        await usersRepository.Received(1).Register(Arg.Is<User>(user =>
            user.Name == request.Name &&
            user.Email == request.Email &&
            user.Password != request.Password));
        await refreshTokenRepository.Received(1).SaveRefreshToken(Arg.Is<RefreshToken>(token =>
            token.Value == expectedRefreshToken));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldPersistHashedPassword_NotPlainText()
    {
        var request = new RequestUserRegisterJsonBuilder()
            .WithPassword("Password123")
            .Build();

        var usersRepository = new UsersRepositoryBuilder()
            .VerifyIfEmailAlreadyExists(false)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder()
            .Generate("refresh-token")
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        await useCase.Execute(request);

        await usersRepository.Received(1).Register(Arg.Is<User>(user =>
            user.Password != request.Password &&
            passwordEncryption.VerifyPassword(request.Password, user.Password)));
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenEmailAlreadyRegistered()
    {
        var request = new RequestUserRegisterJsonBuilder().Build();

        var usersRepository = new UsersRepositoryBuilder()
            .VerifyIfEmailAlreadyExists(true)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.EMAIL_ALREADY_REGISTERED);
        await usersRepository.DidNotReceive().Register(Arg.Any<User>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenRequestIsInvalid()
    {
        var request = new RequestUserRegisterJsonBuilder()
            .WithEmail(string.Empty)
            .Build();

        var usersRepository = new UsersRepositoryBuilder()
            .VerifyIfEmailAlreadyExists(false)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();
        var refreshTokenRepository = new RefreshTokenRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var passwordEncryption = PasswordEncryptionBuilder.Build();

        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.EMAIL_EMPTY);
        await usersRepository.DidNotReceive().Register(Arg.Any<User>());
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UserRegisterUseCase CreateUseCase(
        IUsersRepository usersRepository,
        ITokenServices tokenServices,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        PasswordEncryption passwordEncryption)
    {
        return new UserRegisterUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork, passwordEncryption);
    }
}
