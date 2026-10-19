using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IGoogleIdentityRepository
{
    Task<GoogleLoginChallenge?> GetChallenge(Guid id);
    Task AddChallenge(GoogleLoginChallenge challenge);
    Task PurgeExpiredChallenges(DateTime now);
    Task<GoogleIdentity?> GetBySubject(string issuer, string subject);
    Task<GoogleIdentity?> GetByUserId(Guid userId);
    Task AddIdentity(GoogleIdentity identity);
}
