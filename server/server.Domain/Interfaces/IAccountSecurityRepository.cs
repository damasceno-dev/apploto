using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IAccountSecurityRepository
{
    Task<User?> GetUserForUpdate(Guid id);
    Task RevokeAllRefreshTokens(Guid userId);
    Task RevokeRefreshToken(Guid userId, string value);
    Task InvalidateRecovery(Guid userId);
    Task<PasswordResetToken?> GetResetByHash(string hash);
    Task AddReset(PasswordResetToken token);
    Task<RecoveryThrottle?> GetThrottle(string key);
    Task AddThrottle(RecoveryThrottle throttle);
}
