using NSubstitute;
using server.Application.Services.Security;
using server.Application.UseCases.Platform.Bootstrap;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.AccountSecurity;

public sealed class BootstrapPlatformOperatorUseCaseTest
{
    private const string Email = "first@example.invalid";

    [Fact]
    public async Task EmptyDatabase_ShouldCreateOnlyPasswordlessPlatformUserAndHashedSetupToken()
    {
        var ctx = Create(0);
        ctx.Mail.Send(Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var id = await ctx.UseCase.Execute("Operador", Email, true);
        await ctx.Users.Received(1).Register(Arg.Is<User>(u => u.Id == id && u.Email == Email &&
            u.Name == "Operador" && u.Password == string.Empty && u.IsPlatformOperator && u.EmailVerifiedAt == null));
        await ctx.Security.Received(1).AddReset(Arg.Is<PasswordResetToken>(t => t.UserId == id &&
            t.TokenHash.Length == 64 && t.ExpiresAt == t.CreatedAt.AddMinutes(7)));
        await ctx.Coordination.Received(1).TryAcquire(Arg.Is<string[]>(keys =>
            keys.Contains("platform-first-operator") && keys.Contains(SecurityRules.EmailKey(Email))));
        await ctx.Work.Received(1).Commit(Arg.Any<CancellationToken>());
        await ctx.Scope.Received(1).Complete();
        await ctx.Mail.Received(1).Send(Email, "Ative sua conta de operação",
            Arg.Is<string>(body => body.StartsWith("Para definir sua senha, abra: https://app.example.invalid/reset-password#token=")),
            Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            ctx.Work.Commit(Arg.Any<CancellationToken>());
            ctx.Mail.Send(Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task DryRun_ShouldReadEligibilityButNeverPersistOrSend()
    {
        var ctx = Create(0);
        (await ctx.UseCase.Execute("Operador", Email, false)).ShouldNotBe(Guid.Empty);
        await ctx.Users.DidNotReceive().Register(Arg.Any<User>());
        await ctx.Security.DidNotReceive().AddReset(Arg.Any<PasswordResetToken>());
        await ctx.Work.DidNotReceive().Commit(Arg.Any<CancellationToken>());
        await ctx.Scope.Received(1).Complete();
        await ctx.Mail.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PopulatedDatabase_ShouldRefuseBeforeAnyWrite()
    {
        var ctx = Create(2);
        var error = await Should.ThrowAsync<ConflictException>(() => ctx.UseCase.Execute("Operador", Email, true));
        error.Message.ShouldBe(ResourcesErrorMessages.BOOTSTRAP_FIRST_OPERATOR_ONLY);
        await ctx.Work.DidNotReceive().Commit(Arg.Any<CancellationToken>());
        await ctx.Mail.DidNotReceive().Send(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", Email)]
    [InlineData("Operador", "invalid-email")]
    public async Task InvalidInput_ShouldFailBeforeDatabaseAccess(string name, string email)
    {
        var ctx = Create(0);
        await Should.ThrowAsync<OnValidationException>(() => ctx.UseCase.Execute(name, email, true));
        await ctx.Users.DidNotReceive().CountAll();
        await ctx.Work.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SingleUserWithDifferentEmail_ShouldNotBecomeTheFirstOperator()
    {
        var ctx = Create(1);
        await Should.ThrowAsync<ConflictException>(() => ctx.UseCase.Execute("Operador", Email, true));
        await ctx.Users.DidNotReceive().Register(Arg.Any<User>());
        await ctx.Work.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SingleUnactivatedAccount_ShouldReplaceCapability_AndReportFailedDelivery()
    {
        var ctx = Create(1);
        var prior = new User { Name = "Operador", Email = Email, IsPlatformOperator = true, Password = string.Empty };
        ctx.Users.GetByEmail(Email).Returns(prior);
        ctx.Mail.Send(Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var error = await Should.ThrowAsync<ConflictException>(() => ctx.UseCase.Execute("Operador", Email, true));
        error.Message.ShouldBe(ResourcesErrorMessages.BOOTSTRAP_DELIVERY_FAILED);
        await ctx.Security.Received(1).InvalidateRecovery(prior.Id);
        await ctx.Security.Received(1).AddReset(Arg.Is<PasswordResetToken>(t => t.UserId == prior.Id));
        await ctx.Work.Received(1).Commit(Arg.Any<CancellationToken>());
        await ctx.Users.DidNotReceive().Register(Arg.Any<User>());
    }

    private static Context Create(int userCount)
    {
        var users = Substitute.For<IUsersRepository>();
        users.CountAll().Returns(userCount);
        users.NormalizeEmail(Email).Returns(Email);
        var security = Substitute.For<IAccountSecurityRepository>();
        var scope = Substitute.For<ISecurityScope>();
        var coordination = Substitute.For<ISecurityCoordination>();
        coordination.TryAcquire(Arg.Any<string[]>()).Returns(scope);
        var work = Substitute.For<IUnitOfWork>();
        var mail = Substitute.For<ISecurityMailSender>();
        var options = new AccountSecurityOptions(new Uri("https://app.example.invalid/"),
            TimeSpan.FromMinutes(7), TimeSpan.FromHours(72), TimeSpan.FromSeconds(60));
        var useCase = new BootstrapPlatformOperatorUseCase(users, security, coordination, work, mail, options, TimeProvider.System);
        return new Context(useCase, users, security, coordination, scope, work, mail);
    }

    private sealed record Context(BootstrapPlatformOperatorUseCase UseCase, IUsersRepository Users,
        IAccountSecurityRepository Security, ISecurityCoordination Coordination, ISecurityScope Scope,
        IUnitOfWork Work, ISecurityMailSender Mail);
}
