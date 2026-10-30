using System.Text.Json;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services;
using server.Application.Services.Security;
using server.Application.UseCases.Platform.OperateBranch;
using server.Application.UseCases.Platform.ReadOnboarding;
using server.Application.UseCases.Platform.ReissueOwnerInvitation;
using server.Application.UseCases.Users.Google;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.AccountSecurity;

public sealed class M8PlatformAndGoogleUseCaseTest
{
    private static readonly AccountSecurityOptions Options = new(new Uri("https://app.example.invalid/"),
        TimeSpan.FromMinutes(30), TimeSpan.FromHours(72), TimeSpan.FromSeconds(60));

    [Fact]
    public async Task OperateBranch_IdempotentRead_ShouldUseExactRequestIdAndActorEmailScope()
    {
        var actor = new User { Email = "platform@example.invalid", IsPlatformOperator = true };
        var ownerEmail = "owner@example.invalid";
        var request = new RequestOperateBranchJson { RequestId = Guid.NewGuid(), Name = "Lotérica", OwnerEmail = ownerEmail };
        var security = Substitute.For<IAccountSecurityRepository>();
        security.GetUserForUpdate(actor.Id).Returns(actor);
        var users = Substitute.For<IUsersRepository>();
        users.NormalizeEmail(ownerEmail).Returns(ownerEmail);
        var records = Substitute.For<IOperatedOnboardingRepository>();
        var branch = new Branch { Name = request.Name };
        var invite = new Invitation { BranchId = branch.Id, Email = ownerEmail, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        var hash = SecurityRules.Hash(JsonSerializer.Serialize(new
        {
            request.RequestId, Name = request.Name.Trim(), Cnpj = request.Cnpj?.Trim(),
            Address = request.Address?.Trim(), Phone = request.Phone?.Trim(), Email = ownerEmail,
            Terminals = request.Terminals.Select(x => new { Name = x.Name.Trim(), PairedTabName = x.PairedTabName?.Trim() }),
            Products = request.AdditionalProducts.Select(x => x.Trim()),
            Operators = request.Operators.Select(x => new { Name = x.Name.Trim(), x.TerminalIndexes })
        }));
        records.Get(request.RequestId).Returns(new OperatedOnboarding
        { RequestId = request.RequestId, RequestHash = hash, PlatformUserId = actor.Id,
            BranchId = branch.Id, Branch = branch, OwnerInvitationId = invite.Id, OwnerInvitation = invite });
        var auth = Substitute.For<IAuthenticationService>();
        auth.GetAuthenticatedUser().Returns(actor);
        var coordination = Coordination();
        var work = Substitute.For<IUnitOfWork>();
        var useCase = new OperateBranchUseCase(auth, security, users, records,
            Substitute.For<IOnboardingRepository>(), Substitute.For<IAccountsRepository>(),
            Substitute.For<IOperatorsRepository>(), Substitute.For<IOperatorAccountsRepository>(),
            Substitute.For<IProductsRepository>(), null!,
            new InvitationWorkflow(Substitute.For<IBranchUsersRepository>(), Substitute.For<IOperatorsRepository>(),
                Substitute.For<IOnboardingRepository>(), coordination, work, TimeProvider.System, Options,
                Substitute.For<ISecurityMailSender>()), coordination, work, Substitute.For<ITerminalsRepository>());
        var result = await useCase.Execute(request);
        result.Created.ShouldBeFalse();
        result.Response.BranchId.ShouldBe(branch.Id);
        await records.Received(1).Get(request.RequestId);
        await coordination.Received(1).TryAcquire(Arg.Is<string[]>(keys =>
            keys.Contains(SecurityRules.EmailKey(actor.Email)) && keys.Contains(SecurityRules.EmailKey(ownerEmail))));
        await work.DidNotReceive().Commit();
    }

    [Fact]
    public async Task ReadAndReissue_ShouldUseScopedIds_AndRefuseAcceptedOwnerInvitation()
    {
        var actor = new User { Email = "platform@example.invalid", IsPlatformOperator = true };
        var auth = Substitute.For<IAuthenticationService>();
        auth.GetAuthenticatedUser().Returns(actor);
        var security = Substitute.For<IAccountSecurityRepository>();
        security.GetUserForUpdate(actor.Id).Returns(actor);
        var records = Substitute.For<IOperatedOnboardingRepository>();
        var branch = new Branch { Name = "Lotérica" };
        var invitation = new Invitation { BranchId = branch.Id, Email = "owner@example.invalid",
            Role = Role.Admin, Status = InvitationStatus.Accepted };
        var requestId = Guid.NewGuid();
        records.Get(requestId).Returns(new OperatedOnboarding
        { RequestId = requestId, BranchId = branch.Id, Branch = branch,
            OwnerInvitationId = invitation.Id, OwnerInvitation = invitation });
        var coordination = Coordination();
        var read = new ReadOperatedOnboardingUseCase(auth, security, records, coordination, TimeProvider.System);
        (await read.Execute(requestId)).BranchId.ShouldBe(branch.Id);
        var onboarding = Substitute.For<IOnboardingRepository>();
        onboarding.GetInvitation(invitation.Id, branch.Id).Returns(invitation);
        var work = Substitute.For<IUnitOfWork>();
        var reissue = new ReissueOwnerInvitationUseCase(auth, security, Substitute.For<IUsersRepository>(),
            records, onboarding, coordination, work, null!);
        await Should.ThrowAsync<ConflictException>(() => reissue.Execute(requestId));
        await records.Received(3).Get(requestId);
        await onboarding.Received(1).GetInvitation(invitation.Id, branch.Id);
        await work.DidNotReceive().Commit();
    }

    [Fact]
    public async Task GoogleSignIn_ShouldLookupOnlyVerifiedSubjectAndSuppliedChallengeId()
    {
        var verifier = Substitute.For<IGoogleIdentityVerifier>();
        verifier.IsConfigured.Returns(true);
        verifier.Verify("signed", Arg.Any<CancellationToken>()).Returns(new GoogleVerificationResult(
            new VerifiedGoogleIdentity("https://accounts.google.com", "subject", "user@example.invalid", "nonce", true, true)));
        var identities = Substitute.For<IGoogleIdentityRepository>();
        var users = Substitute.For<IUsersRepository>();
        users.NormalizeEmail("user@example.invalid").Returns("user@example.invalid");
        var coordination = Coordination();
        var work = Substitute.For<IUnitOfWork>();
        var request = new RequestGoogleSignInJson { ChallengeId = Guid.NewGuid(), State = "state", IdToken = "signed" };
        var useCase = new GoogleSignInUseCase(verifier, identities, users, Substitute.For<IRefreshTokenRepository>(),
            Substitute.For<ITokenServices>(), coordination, work, null!, Substitute.For<IAccountSecurityRepository>(),
            new PasswordEncryption(), TimeProvider.System, new LoginAttemptGuardBuilder().Build());
        await Should.ThrowAsync<InvalidLoginException>(() => useCase.Execute(request));
        await identities.Received(1).GetBySubject("https://accounts.google.com", "subject");
        await identities.Received(1).GetChallenge(request.ChallengeId);
        await coordination.Received(1).TryAcquire(Arg.Is<string[]>(keys =>
            keys.Contains("google-challenge:" + request.ChallengeId.ToString("N")) &&
            keys.Contains(SecurityRules.EmailKey("user@example.invalid"))));
        await work.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    private static ISecurityCoordination Coordination()
    {
        var coordination = Substitute.For<ISecurityCoordination>();
        coordination.TryAcquire(Arg.Any<string[]>()).Returns(Substitute.For<ISecurityScope>());
        return coordination;
    }
}
