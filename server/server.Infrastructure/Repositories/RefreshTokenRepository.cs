using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class RefreshTokenRepository(ServerDbContext dbContext) : IRefreshTokenRepository
{
    public string Generate()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }

    public async Task SaveRefreshToken(RefreshToken refreshToken)
    {
        var userRefreshTokens = await dbContext.RefreshTokens.Where(r => r.UserId == refreshToken.UserId).ToListAsync();
        RemoveUserRefreshTokens(userRefreshTokens);
        await dbContext.RefreshTokens.AddAsync(refreshToken);
    }

    public async Task<RefreshToken?> GetRefreshTokenEntity(string value)
    {
        return await dbContext.RefreshTokens.FirstOrDefaultAsync(r => r.Value == value);
    }

    private void RemoveUserRefreshTokens(List<RefreshToken> userRefreshTokens)
    {
        dbContext.RefreshTokens.RemoveRange(userRefreshTokens);
    }
}