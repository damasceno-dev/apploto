namespace server.Domain.Interfaces;

public sealed record VerifiedGoogleIdentity(string Issuer, string Subject, string Email, string Nonce,
    bool EmailVerified, bool GoogleAuthoritative);

public sealed record GoogleVerificationResult(VerifiedGoogleIdentity? Identity, bool ProviderUnavailable = false);

public interface IGoogleIdentityVerifier
{
    bool IsConfigured { get; }
    Task<GoogleVerificationResult> Verify(string idToken, CancellationToken cancellationToken = default);
}
