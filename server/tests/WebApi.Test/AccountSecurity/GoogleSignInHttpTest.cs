using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

// A TEST-ONLY verifier is injected at the signed identity boundary. These scenarios
// exercise real HTTP, PostgreSQL, invitation and refresh transactions, not live Google.
[Collection(ServerApiCollection.Name)]
public sealed class GoogleSignInHttpTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task ChallengeCreation_ShouldPurgeExpiredRowsWithoutTouchingLiveCapabilities()
    {
        var expired = new GoogleLoginChallenge { StateHash = Guid.NewGuid().ToString("N"),
            NonceHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        await factory.UpdateDb(async db => await db.GoogleLoginChallenges.AddAsync(expired));
        var verifier = new TestGoogleVerifier();
        await using var host = Host(verifier);
        using var client = host.CreateClient();
        var created = await Challenge(client);
        (await factory.ReadDb(db => db.GoogleLoginChallenges.AnyAsync(x => x.Id == expired.Id))).ShouldBeFalse();
        (await factory.ReadDb(db => db.GoogleLoginChallenges.AnyAsync(x => x.Id == created.ChallengeId))).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistingPasswordAccount_ShouldRequireProof_LinkOnce_AndUseStableSubjectAfterProviderEmailChanges()
    {
        var verifier = new TestGoogleVerifier();
        await using var host = Host(verifier);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        var challenge = await Challenge(client);
        var token = verifier.Accept(user.Email, "subject-" + Guid.NewGuid().ToString("N"), challenge.Nonce);
        using var denied = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, token));
        denied.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var linked = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, token, password: TestSeeder.DefaultPassword));
        linked.StatusCode.ShouldBe(HttpStatusCode.OK, await linked.Content.ReadAsStringAsync());
        var first = await linked.ReadContentAsync<ResponseUserLoginJson>();
        first.UserId.ShouldBe(user.Id);
        (await factory.ReadDb(db => db.GoogleIdentities.CountAsync(x => x.UserId == user.Id))).ShouldBe(1);
        using var replay = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, token, password: TestSeeder.DefaultPassword));
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var changed = await Challenge(client);
        var changedToken = verifier.Accept($"changed-{Guid.NewGuid():N}@gmail.com", verifier.Subject(token), changed.Nonce);
        using var returning = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(changed, changedToken));
        returning.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returningLogin = await returning.ReadContentAsync<ResponseUserLoginJson>();
        returningLogin.UserId.ShouldBe(user.Id);
        using var renewed = await client.PostAsJsonAsync("/user/renew-token", new RequestUserRenewTokenJson { Value = returningLogin.ResponseToken.RefreshToken });
        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownGoogleIdentity_ShouldOnlyCreateAccountThroughMatchingInvitation()
    {
        var verifier = new TestGoogleVerifier();
        await using var host = Host(verifier);
        using var client = host.CreateClient();
        var email = $"invited-{Guid.NewGuid():N}@gmail.com";
        var unknown = await Challenge(client);
        var subject = "subject-" + Guid.NewGuid().ToString("N");
        var token = verifier.Accept(email, subject, unknown.Nonce);
        using var noInvitation = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(unknown, token, name: "Nova pessoa"));
        noInvitation.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await factory.ReadDb(db => db.Users.CountAsync(x => x.Email == email))).ShouldBe(0);

        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("GoogleInvite");
        using var issued = await client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = email, Role = Role.Member }, admin);
        issued.StatusCode.ShouldBe(HttpStatusCode.Created);
        var capability = await host.Services.GetRequiredService<SecurityMailCapture>().WaitToken(email);
        var fresh = await Challenge(client);
        var freshToken = verifier.Accept(email, subject, fresh.Nonce);
        using var accepted = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(fresh, freshToken, capability, "Nova pessoa"));
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        var identity = await accepted.ReadContentAsync<ResponseUserLoginJson>();
        identity.UserId.ShouldNotBe(Guid.Empty);
        (await factory.ReadDb(db => db.BranchUsers.SingleAsync(x => x.BranchId == branch.Id && x.UserId == identity.UserId))).Role.ShouldBe(Role.Member);
        (await factory.ReadDb(db => db.Users.SingleAsync(x => x.Id == identity.UserId))).Password.ShouldBe(string.Empty);
        using var localPassword = await client.PostAsJsonAsync("/user/login", new RequestUserLoginJson { Email = email, Password = TestSeeder.DefaultPassword });
        localPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var invitationReplay = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(fresh, freshToken, capability));
        invitationReplay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Challenge_ShouldFailClosedWithoutConfiguredProvider_AndRejectInvalidAssertionWithVerifier()
    {
        using var ordinary = factory.CreateClient();
        using var disabled = await ordinary.PostAsync("/user/google/challenge", null);
        disabled.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var verifier = new TestGoogleVerifier();
        await using var host = Host(verifier);
        using var client = host.CreateClient();
        var challenge = await Challenge(client);
        using var invalid = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, "not-a-google-token"));
        invalid.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var email = $"proof-{Guid.NewGuid():N}@gmail.com";
        var user = await factory.SeedUserAsync(email);
        var token = verifier.Accept(email, "subject-" + Guid.NewGuid().ToString("N"), challenge.Nonce, verified: false);
        using var unverified = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, token, password: TestSeeder.DefaultPassword));
        unverified.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await factory.ReadDb(db => db.GoogleIdentities.CountAsync(x => x.UserId == user.Id))).ShouldBe(0);
    }

    [Fact]
    public async Task DifferentSubjectsRacingForOnePasswordAccount_ShouldLinkOnlyOne()
    {
        var verifier = new TestGoogleVerifier();
        await using var host = Host(verifier);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        var firstChallenge = await Challenge(client);
        var secondChallenge = await Challenge(client);
        var firstToken = verifier.Accept(user.Email, "subject-" + Guid.NewGuid().ToString("N"), firstChallenge.Nonce);
        var secondToken = verifier.Accept(user.Email, "subject-" + Guid.NewGuid().ToString("N"), secondChallenge.Nonce);
        var first = client.PostAsJsonAsync("/user/google/sign-in", SignIn(firstChallenge, firstToken, password: TestSeeder.DefaultPassword));
        var second = client.PostAsJsonAsync("/user/google/sign-in", SignIn(secondChallenge, secondToken, password: TestSeeder.DefaultPassword));
        using var a = await first;
        using var b = await second;
        new[] { a.StatusCode, b.StatusCode }.Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        (await factory.ReadDb(db => db.GoogleIdentities.CountAsync(x => x.UserId == user.Id))).ShouldBe(1);
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(x => x.UserId == user.Id && x.Active))).ShouldBe(1);
    }

    [Fact]
    public async Task WrongStateNonceAndInvitationEmail_ShouldNotConsumeChallengeOrCreateAccount()
    {
        var verifier = new TestGoogleVerifier();
        await using var host = Host(verifier);
        using var client = host.CreateClient();
        var challenge = await Challenge(client);
        var email = $"identity-{Guid.NewGuid():N}@gmail.com";
        var token = verifier.Accept(email, "subject-" + Guid.NewGuid().ToString("N"), challenge.Nonce);
        using var wrongState = await client.PostAsJsonAsync("/user/google/sign-in", new RequestGoogleSignInJson
        { ChallengeId = challenge.ChallengeId, State = "wrong", IdToken = token });
        wrongState.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var wrongNonceToken = verifier.Accept(email, "subject-" + Guid.NewGuid().ToString("N"), "wrong-nonce");
        using var wrongNonce = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, wrongNonceToken));
        wrongNonce.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var (_, _, _, admin) = await factory.SeedFullBranchContextAsync("GoogleMismatch");
        var otherEmail = $"other-{Guid.NewGuid():N}@gmail.com";
        using var issued = await client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = otherEmail, Role = Role.Member }, admin);
        issued.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invitation = await host.Services.GetRequiredService<SecurityMailCapture>().WaitToken(otherEmail);
        using var mismatch = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, token, invitation, "Pessoa"));
        mismatch.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ReadDb(db => db.Users.CountAsync(x => x.Email == email))).ShouldBe(0);
        (await factory.ReadDb(db => db.GoogleLoginChallenges.SingleAsync(x => x.Id == challenge.ChallengeId))).ConsumedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RemovedMembershipAndInactiveAccount_ShouldRemainDeniedAfterGoogleSignIn()
    {
        var verifier = new TestGoogleVerifier();
        await using var host = Host(verifier);
        using var client = host.CreateClient();
        var (_, branch, _, adminToken) = await factory.SeedFullBranchContextAsync("GoogleRemoval");
        var member = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(member.Id, branch.Id, Role.Member);
        var subject = "subject-" + Guid.NewGuid().ToString("N");
        var challenge = await Challenge(client);
        var token = verifier.Accept(member.Email, subject, challenge.Nonce);
        using var linked = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(challenge, token, password: TestSeeder.DefaultPassword));
        linked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var identity = await linked.ReadContentAsync<ResponseUserLoginJson>();
        using var activeSession = await client.PostAuthAsync("/branch/session", new RequestCreateBranchSessionJson { BranchId = branch.Id }, identity.ResponseToken.Token);
        activeSession.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var removed = await client.DeleteAuthAsync($"/branch/users/{membership.Id}", adminToken);
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var deniedSession = await client.PostAuthAsync("/branch/session", new RequestCreateBranchSessionJson { BranchId = branch.Id }, identity.ResponseToken.Token);
        deniedSession.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var returningChallenge = await Challenge(client);
        var returningToken = verifier.Accept(member.Email, subject, returningChallenge.Nonce);
        using var returning = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(returningChallenge, returningToken));
        returning.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returningIdentity = await returning.ReadContentAsync<ResponseUserLoginJson>();
        using var stillDenied = await client.PostAuthAsync("/branch/session", new RequestCreateBranchSessionJson { BranchId = branch.Id }, returningIdentity.ResponseToken.Token);
        stillDenied.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
            (await db.Users.FindAsync(member.Id))!.Active = false;
            await db.SaveChangesAsync();
        }
        var inactiveChallenge = await Challenge(client);
        var inactiveToken = verifier.Accept(member.Email, subject, inactiveChallenge.Nonce);
        using var inactive = await client.PostAsJsonAsync("/user/google/sign-in", SignIn(inactiveChallenge, inactiveToken));
        inactive.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(TestGoogleVerifier verifier) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGoogleIdentityVerifier>();
            services.AddSingleton<IGoogleIdentityVerifier>(verifier);
        }));

    private static async Task<ResponseGoogleChallengeJson> Challenge(HttpClient client)
    {
        using var response = await client.PostAsync("/user/google/challenge", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseGoogleChallengeJson>();
    }

    private static RequestGoogleSignInJson SignIn(ResponseGoogleChallengeJson challenge, string idToken,
        string? invitation = null, string? name = null, string? password = null) => new()
    {
        ChallengeId = challenge.ChallengeId, State = challenge.State, IdToken = idToken,
        InvitationToken = invitation, Name = name, CurrentPassword = password
    };

    private sealed class TestGoogleVerifier : IGoogleIdentityVerifier
    {
        private readonly ConcurrentDictionary<string, VerifiedGoogleIdentity> values = new();
        public bool IsConfigured => true;
        public string Accept(string email, string subject, string nonce, bool verified = true)
        {
            var key = Guid.NewGuid().ToString("N");
            values[key] = new VerifiedGoogleIdentity("https://accounts.google.com", subject, email, nonce,
                verified, email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase));
            return key;
        }
        public string Subject(string key) => values[key].Subject;
        public Task<GoogleVerificationResult> Verify(string idToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GoogleVerificationResult(values.GetValueOrDefault(idToken)));
    }
}
