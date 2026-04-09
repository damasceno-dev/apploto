using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class BranchesRepository(ServerDbContext dbContext) : IBranchesRepository
{
    public async Task Add(Branch branch)
    {
        await dbContext.Branches.AddAsync(branch);
    }

    public async Task<Branch?> GetById(Guid branchId)
    {
        return await dbContext.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(branch => branch.Id == branchId && branch.Active);
    }
}
