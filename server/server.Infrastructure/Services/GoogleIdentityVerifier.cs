using Google.Apis.Auth;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

// The Google library owns key retrieval, signature, issuer, audience and lifetime verification.
// Production never accepts a caller-supplied issuer, certificate URL or verification key.
internal sealed class GoogleIdentityVerifier : IGoogleIdentityVerifier
{
    private readonly string[] clientIds;

    public GoogleIdentityVerifier(string[] clientIds) => this.clientIds = clientIds
        .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToArray();

    public bool IsConfigured => clientIds.Length > 0;

    public async Task<GoogleVerificationResult> Verify(string idToken, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return new GoogleVerificationResult(null, ProviderUnavailable: true);
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 16384) return new GoogleVerificationResult(null);
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = clientIds });
            return new GoogleVerificationResult(GoogleIdentityPolicy.Accept(payload));
        }
        catch (InvalidJwtException)
        {
            return new GoogleVerificationResult(null);
        }
        catch (ArgumentException)
        {
            return new GoogleVerificationResult(null);
        }
        catch (FormatException)
        {
            return new GoogleVerificationResult(null);
        }
        catch (HttpRequestException)
        {
            return new GoogleVerificationResult(null, ProviderUnavailable: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GoogleVerificationResult(null, ProviderUnavailable: true);
        }
    }
}
