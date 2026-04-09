using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class BranchUsersRepository(ServerDbContext dbContext) : IBranchUsersRepository
{
    public async Task Add(BranchUser branchUser)
    {
        await dbContext.BranchUsers.AddAsync(branchUser);
    }

    public async Task<BranchUser?> GetActiveById(Guid branchUserId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(branchUser => branchUser.Id == branchUserId && branchUser.Active);
    }

    public async Task<BranchUser?> GetActiveByUserIdAndBranchId(Guid userId, Guid branchId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(branchUser =>
                branchUser.UserId == userId &&
                branchUser.BranchId == branchId &&
                branchUser.Active);
    }

    public async Task<BranchUser?> GetByUserIdAndBranchId(Guid userId, Guid branchId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(branchUser => branchUser.UserId == userId && branchUser.BranchId == branchId);
    }
}
