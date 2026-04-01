using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    string Generate();
    Task SaveRefreshToken(RefreshToken refreshToken);
    Task<RefreshToken?> GetRefreshTokenEntity(string value);
}