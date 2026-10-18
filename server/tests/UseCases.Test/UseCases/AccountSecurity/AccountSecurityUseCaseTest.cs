using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using server.Application.Services;
using server.Application.Services.Security;
using server.Application.UseCases.Users.ChangePassword;
using server.Application.UseCases.Users.Logout;
using server.Application.UseCases.Users.ForgotPassword;
using server.Application.UseCases.Users.ResetPassword;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.AccountSecurity;

public sealed class AccountSecurityUseCaseTest
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ChangePassword_ShouldRevokeAllSessionsAndRecoveryUnderTheAccountKey()
    {
        var ctx = Create();
        await ctx.ChangePassword.Execute(new RequestChangePasswordJson { CurrentPassword = "Password123", NewPassword = "NewPassword123" });
        await ctx.Security.Received(1).RevokeAllRefreshTokens(ctx.User.Id);
        await ctx.Security.Received(1).InvalidateRecovery(ctx.User.Id);
        await ctx.Coordination.Received(1).TryAcquire(Arg.Is<string[]>(keys => keys.SequenceEqual(new[] { SecurityRules.EmailKey(ctx.User.Email) })));
        new PasswordEncryption().VerifyPassword("NewPassword123", ctx.User.Password).ShouldBeTrue();
        await ctx.Work.Received(1).Commit();
        await ctx.Scope.Received(1).Complete();
    }

    [Fact]
    public async Task WrongCurrentPassword_ShouldNotMutateOrRevoke()
    {
        var ctx = Create();
        var original = ctx.User.Password;
        await Should.ThrowAsync<InvalidLoginException>(() => ctx.ChangePassword.Execute(new RequestChangePasswordJson
        { CurrentPassword = "wrong", NewPassword = "NewPassword123" }));
        ctx.User.Password.ShouldBe(original);
        await ctx.Security.DidNotReceive().RevokeAllRefreshTokens(ctx.User.Id);
        await ctx.Work.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Logout_ShouldPassOnlyAuthenticatedUserAndContractedCredentialToRevocation()
    {
        var ctx = Create();
        await ctx.Logout.Execute(new RequestLogoutJson { RefreshToken = "contracted-refresh" });
        await ctx.Security.Received(1).RevokeRefreshToken(ctx.User.Id, "contracted-refresh");
        await ctx.Security.DidNotReceive().RevokeAllRefreshTokens(Arg.Any<Guid>());
        await ctx.Security.Received(1).GetUserForUpdate(ctx.User.Id);
        await ctx.Work.Received(1).Commit();
        await ctx.Scope.Received(1).Complete();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidSecurityRequest_ShouldFailBeforeAccountLockOrRevocation(bool changePassword)
    {
        var ctx = Create();
        Func<Task> execute = changePassword
            ? () => ctx.ChangePassword.Execute(new RequestChangePasswordJson { CurrentPassword = "Password123", NewPassword = "short" })
            : () => ctx.Logout.Execute(new RequestLogoutJson { RefreshToken = "" });
        await Should.ThrowAsync<OnValidationException>(execute);
        await ctx.Coordination.DidNotReceive().TryAcquire(Arg.Any<string[]>());
        await ctx.Security.DidNotReceive().RevokeAllRefreshTokens(Arg.Any<Guid>());
        await ctx.Security.DidNotReceive().RevokeRefreshToken(Arg.Any<Guid>(), Arg.Any<string>());
        await ctx.Work.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Logout_ShouldRecheckAccountUnderLock_BeforeRevokingOrCommitting()
    {
        var ctx = Create();
        ctx.Security.GetUserForUpdate(ctx.User.Id).Returns((User?)null);
        await Should.ThrowAsync<TokenWithoutUserException>(() => ctx.Logout.Execute(new RequestLogoutJson { RefreshToken = "contracted-refresh" }));
        await ctx.Security.DidNotReceive().RevokeRefreshToken(Arg.Any<Guid>(), Arg.Any<string>());
        await ctx.Work.DidNotReceive().Commit();
        await ctx.Scope.DidNotReceive().Complete();
    }

    [Fact]
    public async Task ForgotPassword_ShouldUseServerUtcHashedCapabilityAndQueueOnlyAfterCommit()
    {
        var ctx = Create();
        ctx.Users.GetByEmail(ctx.User.Email).Returns(ctx.User);
        await ctx.ForgotPassword.Execute(new RequestForgotPasswordJson { Email = ctx.User.Email });
        await ctx.Security.Received(1).AddReset(Arg.Is<PasswordResetToken>(t => t.UserId == ctx.User.Id && t.TokenHash.Length == 64 &&
            t.CreatedAt == Now.UtcDateTime && t.ExpiresAt == Now.UtcDateTime.AddMinutes(30)));
        Received.InOrder(() =>
        {
            ctx.Work.Commit();
            ctx.Mail.TryEnqueue(ctx.User.Email, Arg.Is<string>(body => body.StartsWith("Para redefinir sua senha, abra: https://app.example.invalid/reset-password#token=")));
        });
    }

    [Fact]
    public async Task FailedRecoveryCommit_ShouldNotQueuePlaintextCapability()
    {
        var ctx = Create();
        ctx.Users.GetByEmail(ctx.User.Email).Returns(ctx.User);
        ctx.Work.Commit().ThrowsAsync(new InvalidOperationException("test commit failed"));
        await Should.ThrowAsync<InvalidOperationException>(() => ctx.ForgotPassword.Execute(new RequestForgotPasswordJson { Email = ctx.User.Email }));
        ctx.Mail.DidNotReceive().TryEnqueue(Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task ResetExpiry_ShouldUseInjectedUtc_AndRejectTheExactBoundary(int seconds)
    {
        var ctx = Create();
        var capability = SecurityRules.NewToken();
        ctx.Security.GetResetByHash(SecurityRules.Hash(capability)).Returns(new PasswordResetToken
        { UserId = ctx.User.Id, TokenHash = SecurityRules.Hash(capability), ExpiresAt = Now.UtcDateTime.AddSeconds(seconds) });
        ctx.Users.GetById(ctx.User.Id).Returns(ctx.User);
        var error = await Should.ThrowAsync<ConflictException>(() => ctx.ResetPassword.Execute(new RequestResetPasswordJson
        { Token = capability, NewPassword = "NewPassword123" }));
        error.Message.ShouldBe(ResourcesErrorMessages.SECURITY_TOKEN_INVALID);
        await ctx.Work.DidNotReceive().Commit();
    }

    private static Context Create()
    {
        var password = new PasswordEncryption();
        var user = new UserBuilder().WithEmail("person@example.invalid").Build();
        user.Password = password.HashPassword("Password123");
        var auth = new AuthenticationServiceBuilder().GetAuthenticatedUser(user).Build();
        var users = new UsersRepositoryBuilder().Build();
        var security = Substitute.For<IAccountSecurityRepository>();
        security.GetUserForUpdate(user.Id).Returns(user);
        var scope = Substitute.For<ISecurityScope>();
        var coordination = SecurityCoordinationBuilder.Build(scope);
        var work = new UnitOfWorkBuilder().Build();
        var mail = Substitute.For<IRecoveryMailQueue>();
        var clock = new FixedUtcTimeProvider(Now);
        var options = new AccountSecurityOptions(new Uri("https://app.example.invalid/"), TimeSpan.FromMinutes(30), TimeSpan.FromHours(72), TimeSpan.FromSeconds(60));
        var updater = new AccountPasswordUpdater(security, password);
        return new(user, users, security, coordination, scope, work, mail,
            new ChangeUserPasswordUseCase(auth, security, coordination, work, password, updater),
            new LogoutUserUseCase(auth, security, coordination, work),
            new ForgotUserPasswordUseCase(users, security, coordination, work, clock, options, mail),
            new ResetUserPasswordUseCase(users, security, coordination, work, clock, updater));
    }
    private sealed record Context(User User, IUsersRepository Users, IAccountSecurityRepository Security,
        ISecurityCoordination Coordination, ISecurityScope Scope, IUnitOfWork Work, IRecoveryMailQueue Mail,
        ChangeUserPasswordUseCase ChangePassword, LogoutUserUseCase Logout,
        ForgotUserPasswordUseCase ForgotPassword, ResetUserPasswordUseCase ResetPassword);
}
