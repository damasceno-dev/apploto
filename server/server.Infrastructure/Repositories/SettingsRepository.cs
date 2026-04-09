using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class SettingsRepository(ServerDbContext dbContext) : ISettingsRepository
{
    public async Task Add(Setting setting)
    {
        await dbContext.Settings.AddAsync(setting);
    }
}
