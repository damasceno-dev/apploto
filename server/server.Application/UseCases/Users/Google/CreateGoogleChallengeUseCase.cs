using server.Application.Services.Security;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.Google;

public sealed class CreateGoogleChallengeUseCase(IGoogleIdentityVerifier verifier, IGoogleIdentityRepository identities,
    IUnitOfWork unitOfWork, TimeProvider time)
{
    public async Task<ResponseGoogleChallengeJson> Execute()
    {
        if (!verifier.IsConfigured)
            throw new GoogleProviderUnavailableException(ResourcesErrorMessages.GOOGLE_NOT_CONFIGURED);
        var now = time.GetUtcNow().UtcDateTime;
        await identities.PurgeExpiredChallenges(now);
        var state = SecurityRules.NewToken();
        var nonce = SecurityRules.NewToken();
        var challenge = new GoogleLoginChallenge
        {
            StateHash = SecurityRules.Hash(state), NonceHash = SecurityRules.Hash(nonce), ExpiresAt = now.AddMinutes(5)
        };
        await identities.AddChallenge(challenge);
        await unitOfWork.Commit();
        return new ResponseGoogleChallengeJson
        {
            ChallengeId = challenge.Id, State = state, Nonce = nonce, ExpiresAt = challenge.ExpiresAt
        };
    }
}
