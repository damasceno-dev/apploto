using Google.Apis.Auth;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal static class GoogleIdentityPolicy
{
    // This function receives only a payload already authenticated by Google's verifier.
    internal static VerifiedGoogleIdentity? Accept(GoogleJsonWebSignature.Payload payload)
    {
        if (payload.EmailVerified != true || string.IsNullOrWhiteSpace(payload.Email) ||
            string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Nonce) ||
            payload.Issuer is not ("accounts.google.com" or "https://accounts.google.com")) return null;
        var authoritative = payload.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) ||
                            !string.IsNullOrWhiteSpace(payload.HostedDomain);
        return new VerifiedGoogleIdentity("https://accounts.google.com", payload.Subject,
            payload.Email, payload.Nonce, true, authoritative);
    }
}
