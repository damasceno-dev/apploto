using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal sealed class GoogleIdentityRepository(ServerDbContext db) : IGoogleIdentityRepository
{
    public async Task<GoogleLoginChallenge?> GetChallenge(Guid id)
    {
        var result = await db.GoogleLoginChallenges.SingleOrDefaultAsync(x => x.Id == id);
        if (result is not null) await db.Entry(result).ReloadAsync();
        return result;
    }
    public async Task AddChallenge(GoogleLoginChallenge challenge) => await db.GoogleLoginChallenges.AddAsync(challenge);
    public async Task PurgeExpiredChallenges(DateTime now) => await db.GoogleLoginChallenges
        .Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync();
    public async Task<GoogleIdentity?> GetBySubject(string issuer, string subject)
    {
        var result = await db.GoogleIdentities.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject);
        if (result is not null) await db.Entry(result).ReloadAsync();
        return result;
    }
    public Task<GoogleIdentity?> GetByUserId(Guid userId) => db.GoogleIdentities.SingleOrDefaultAsync(x => x.UserId == userId);
    public async Task AddIdentity(GoogleIdentity identity) => await db.GoogleIdentities.AddAsync(identity);
}
