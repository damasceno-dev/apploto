using Google.Apis.Auth;
using server.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace WebApi.Test.AccountSecurity;

public sealed class GoogleVerifierBoundaryTest
{
    [Theory]
    [InlineData("person@gmail.com", null, true)]
    [InlineData("person@shop.example", "shop.example", true)]
    [InlineData("person@shop.example", null, false)]
    public void ValidatedPayloadPolicy_ShouldClassifyMailboxAuthority(string email, string? hostedDomain, bool authoritative)
    {
        var payload = new GoogleJsonWebSignature.Payload
        {
            Issuer = "https://accounts.google.com", Subject = "stable-subject", Email = email,
            EmailVerified = true, Nonce = "signed-nonce", HostedDomain = hostedDomain
        };
        var identity = GoogleIdentityPolicy.Accept(payload);
        identity.ShouldNotBeNull();
        identity.GoogleAuthoritative.ShouldBe(authoritative);
        payload.EmailVerified = false;
        GoogleIdentityPolicy.Accept(payload).ShouldBeNull();
        payload.EmailVerified = true;
        payload.Issuer = "https://attacker.invalid";
        GoogleIdentityPolicy.Accept(payload).ShouldBeNull();
        payload.Issuer = "https://accounts.google.com";
        payload.Nonce = "";
        GoogleIdentityPolicy.Accept(payload).ShouldBeNull();
        payload.Nonce = "signed-nonce";
        payload.Subject = "";
        GoogleIdentityPolicy.Accept(payload).ShouldBeNull();
    }
    [Fact]
    public async Task ProductionVerifier_ShouldFailClosedWithoutAudience_AndRejectUnsignedOrMalformedAssertions()
    {
        var disabled = new GoogleIdentityVerifier([]);
        disabled.IsConfigured.ShouldBeFalse();
        (await disabled.Verify("irrelevant")).ProviderUnavailable.ShouldBeTrue();

        var configured = new GoogleIdentityVerifier(["test-client.apps.googleusercontent.com"]);
        configured.IsConfigured.ShouldBeTrue();
        (await configured.Verify("not-a-jwt")).Identity.ShouldBeNull();
        // alg=none is never a production identity, even with an otherwise parseable payload.
        (await configured.Verify("eyJhbGciOiJub25lIn0.eyJzdWIiOiJzIn0.")).Identity.ShouldBeNull();
    }
}
