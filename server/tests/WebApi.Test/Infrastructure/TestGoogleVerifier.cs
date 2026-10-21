using System.Collections.Concurrent;
using server.Domain.Interfaces;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// A TEST-ONLY verifier injected at the signed identity boundary: it accepts only assertions a test registered. The
/// scenarios that use it exercise real HTTP, PostgreSQL, invitation and refresh transactions, not live Google.
/// </summary>
internal sealed class TestGoogleVerifier : IGoogleIdentityVerifier
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
