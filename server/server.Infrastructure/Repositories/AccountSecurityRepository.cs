using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal sealed class AccountSecurityRepository(ServerDbContext db) : IAccountSecurityRepository
{
    public Task<User?> GetUserForUpdate(Guid id) => db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Active);
    public async Task RevokeAllRefreshTokens(Guid userId)
    {
        foreach (var token in await db.RefreshTokens.Where(t => t.UserId == userId && t.Active).ToListAsync()) token.Active = false;
    }
    public async Task RevokeRefreshToken(Guid userId, string value)
    {
        var sessionId = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId && t.Value == value)
            .Select(t => (Guid?)t.SessionId).SingleOrDefaultAsync();
        if (!sessionId.HasValue) return;
        foreach (var token in await db.RefreshTokens.Where(t => t.UserId == userId && t.SessionId == sessionId && t.Active).ToListAsync())
            token.Active = false;
    }
    public async Task InvalidateRecovery(Guid userId)
    {
        foreach (var token in await db.PasswordResetTokens.Where(t => t.UserId == userId && t.Active).ToListAsync())
            token.Active = false;
    }
    public async Task<PasswordResetToken?> GetResetByHash(string hash)
    {
        var token = await db.PasswordResetTokens.SingleOrDefaultAsync(t => t.TokenHash == hash);
        if (token is not null) await db.Entry(token).ReloadAsync();
        return token;
    }
    public async Task AddReset(PasswordResetToken token) => await db.PasswordResetTokens.AddAsync(token);
    // Row-locked for the same reason as LoginThrottleRepository: the retention sweep skips it while a request holds it.
    public Task<RecoveryThrottle?> GetThrottle(string key) => db.RecoveryThrottles
        .FromSql($"SELECT * FROM \"RecoveryThrottles\" WHERE \"KeyHash\" = {key} FOR UPDATE")
        .SingleOrDefaultAsync();
    public async Task AddThrottle(RecoveryThrottle throttle) => await db.RecoveryThrottles.AddAsync(throttle);
}
