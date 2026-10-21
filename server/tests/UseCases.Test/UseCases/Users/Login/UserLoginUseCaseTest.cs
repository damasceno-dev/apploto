using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services;
using server.Application.Services.Security;
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

        var scope = Substitute.For<ISecurityScope>();
        var useCase = CreateUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork,
            passwordEncryption, SecurityCoordinationBuilder.Build(scope));

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(user.Name);
        response.Email.ShouldBe(user.Email);
        response.UserId.ShouldBe(user.Id);
        response.ResponseToken.Token.ShouldBe(expectedToken);
        response.ResponseToken.RefreshToken.ShouldBe(expectedRefreshToken);
        await refreshTokenRepository.Received(1).SaveRefreshToken(Arg.Is<RefreshToken>(token =>
            token.Value == expectedRefreshToken && token.UserId == user.Id));
        await unitOfWork.Received(1).Commit();
        await scope.Received(1).Complete();
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

        exception.Message.ShouldBe(ResourcesErrorMessages.PASSWORD_WRONG);
        // The failed check is counted for a missing account exactly as for an existing one.
        await unitOfWork.Received(1).Commit();
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
        await unitOfWork.Received(1).Commit();
        await refreshTokenRepository.DidNotReceive().SaveRefreshToken(Arg.Any<RefreshToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Execute_ShouldUseTheSameCredentialsError_ForInactiveOrPasswordlessAccount(bool active, bool passwordless)
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithHashedPassword("Password123").Build();
        user.Active = active;
        if (passwordless) user.Password = string.Empty;
        var request = new RequestUserLoginJsonBuilder().WithEmail(user.Email).WithPassword("Password123").Build();
        var users = new UsersRepositoryBuilder().GetByEmail(user).Build();
        var work = new UnitOfWorkBuilder().Build();
        var useCase = CreateUseCase(users, new TokenServicesBuilder().Build(),
            new RefreshTokenRepositoryBuilder().Build(), work, PasswordEncryptionBuilder.Build());

        var error = await Should.ThrowAsync<InvalidLoginException>(() => useCase.Execute(request));
        error.Message.ShouldBe(ResourcesErrorMessages.PASSWORD_WRONG);
        await work.Received(1).Commit();
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

    [Fact]
    public async Task Execute_ShouldCountAFailure_InBothBudgets_ForEveryAccountState()
    {
        var request = new RequestUserLoginJsonBuilder().WithEmail("absent@example.com").WithPassword("WrongPassword123").Build();
        var users = new UsersRepositoryBuilder().GetByEmail(null).Build();
        users.NormalizeEmail(request.Email).Returns("absent@example.com");
        var guard = new LoginAttemptGuardBuilder();
        var work = new UnitOfWorkBuilder().Build();
        var scope = Substitute.For<ISecurityScope>();
        var useCase = CreateUseCase(users, new TokenServicesBuilder().Build(), new RefreshTokenRepositoryBuilder().Build(), work,
            PasswordEncryptionBuilder.Build(), SecurityCoordinationBuilder.Build(scope), guard.Build());

        await Should.ThrowAsync<InvalidLoginException>(() => useCase.Execute(request));

        guard.Throttles.Rows.Count.ShouldBe(2);
        guard.Throttles.Rows.ShouldAllBe(r => r.FailureCount == 1 && r.AccountKeyHash == LoginAttemptGuard.AccountKey("absent@example.com"));
        await work.Received(1).Commit();
        await scope.Received(1).Complete();
    }

    [Fact]
    public async Task Execute_ShouldRefuseBeforeReadingTheAccount_WhenABudgetIsBlocked()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithHashedPassword("Password123").Build();
        var request = new RequestUserLoginJsonBuilder().WithEmail(user.Email).WithPassword("Password123").Build();
        var users = new UsersRepositoryBuilder().GetByEmail(user).Build();
        users.NormalizeEmail(request.Email).Returns(user.Email);
        var guard = new LoginAttemptGuardBuilder();
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero).UtcDateTime;
        guard.Throttles.WithRow(new LoginThrottle
        {
            KeyHash = LoginAttemptGuard.AccountKey(user.Email), AccountKeyHash = LoginAttemptGuard.AccountKey(user.Email),
            FailureCount = LoginProtectionOptionsFactory.DefaultAccountFailureLimit, WindowStartedAt = now.AddMinutes(-1),
            BlockedUntil = now.AddSeconds(90), ExpiresAt = now.AddSeconds(90)
        });
        var refresh = new RefreshTokenRepositoryBuilder().Build();
        var work = new UnitOfWorkBuilder().Build();
        var useCase = CreateUseCase(users, new TokenServicesBuilder().Build(), refresh, work, PasswordEncryptionBuilder.Build(),
            attempts: guard.Build());

        var error = await Should.ThrowAsync<TooManyAttemptsException>(() => useCase.Execute(request));

        error.Message.ShouldBe(ResourcesErrorMessages.SECURITY_RATE_LIMITED);
        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(90));
        await users.DidNotReceive().GetByEmail(Arg.Any<string>());
        await refresh.DidNotReceive().SaveRefreshToken(Arg.Any<RefreshToken>());
        await work.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldClearOnlyThisNetworksBudget_WhenThePasswordIsProven()
    {
        var user = new UserBuilder().WithEmail("user@example.com").WithHashedPassword("Password123").Build();
        var request = new RequestUserLoginJsonBuilder().WithEmail(user.Email).WithPassword("WrongPassword123").Build();
        var users = new UsersRepositoryBuilder().GetByEmail(user).Build();
        users.NormalizeEmail(Arg.Any<string>()).Returns(user.Email);
        var guard = new LoginAttemptGuardBuilder();
        var attempts = guard.Build();
        var useCase = CreateUseCase(users, new TokenServicesBuilder().Build(), new RefreshTokenRepositoryBuilder().Build(),
            new UnitOfWorkBuilder().Build(), PasswordEncryptionBuilder.Build(), attempts: attempts);
        await Should.ThrowAsync<InvalidLoginException>(() => useCase.Execute(request));

        await useCase.Execute(new RequestUserLoginJsonBuilder().WithEmail(user.Email).WithPassword("Password123").Build());

        var remaining = guard.Throttles.Rows.ShouldHaveSingleItem();
        remaining.KeyHash.ShouldBe(LoginAttemptGuard.AccountKey(user.Email));
        remaining.FailureCount.ShouldBe(1);
    }

    private static UserLoginUseCase CreateUseCase(
        IUsersRepository usersRepository,
        ITokenServices tokenServices,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        PasswordEncryption passwordEncryption,
        ISecurityCoordination? coordination = null,
        LoginAttemptGuard? attempts = null)
    {
        return new UserLoginUseCase(usersRepository, tokenServices, refreshTokenRepository, unitOfWork,
            passwordEncryption, coordination ?? SecurityCoordinationBuilder.Build(), attempts ?? new LoginAttemptGuardBuilder().Build());
    }
}
