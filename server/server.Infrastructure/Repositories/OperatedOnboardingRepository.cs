using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal sealed class OperatedOnboardingRepository(ServerDbContext db) : IOperatedOnboardingRepository
{
    public Task<OperatedOnboarding?> Get(Guid requestId) => db.OperatedOnboardings
        .Include(x => x.Branch).Include(x => x.OwnerInvitation).SingleOrDefaultAsync(x => x.RequestId == requestId);
    public async Task Add(OperatedOnboarding record) => await db.OperatedOnboardings.AddAsync(record);
}
