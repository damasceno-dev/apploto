using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class RefreshTokenRepository(ServerDbContext dbContext) : IRefreshTokenRepository
{
    public string Generate()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public async Task SaveRefreshToken(RefreshToken refreshToken)
    {
        var userRefreshTokens = await dbContext.RefreshTokens.Where(r => r.UserId == refreshToken.UserId && r.Active).ToListAsync();
        foreach (var previous in userRefreshTokens) previous.Active = false;
        await dbContext.RefreshTokens.AddAsync(refreshToken);
    }

    public async Task<RefreshToken?> GetRefreshTokenEntity(string value)
    {
        return await dbContext.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(r => r.Value == value && r.Active);
    }

}
