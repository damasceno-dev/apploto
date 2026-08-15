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

    public async Task<BranchUser?> GetById(Guid branchUserId)
    {
        return await dbContext.BranchUsers
            .Include(branchUser => branchUser.User)
            .FirstOrDefaultAsync(branchUser => branchUser.Id == branchUserId && branchUser.Branch.Active);
    }

    public async Task<BranchUser?> GetActiveById(Guid branchUserId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .Include(branchUser => branchUser.User)
            .FirstOrDefaultAsync(branchUser =>
                branchUser.Id == branchUserId &&
                branchUser.Active &&
                branchUser.Branch.Active);
    }

    public async Task<BranchUser?> GetActiveByUserIdAndBranchId(Guid userId, Guid branchId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(branchUser =>
                branchUser.UserId == userId &&
                branchUser.BranchId == branchId &&
                branchUser.Active &&
                branchUser.Branch.Active);
    }

    public async Task<BranchUser?> GetByUserIdAndBranchId(Guid userId, Guid branchId)
    {
        return await dbContext.BranchUsers
            .FirstOrDefaultAsync(branchUser => branchUser.UserId == userId && branchUser.BranchId == branchId);
    }

    public async Task<int> CountActiveAdminsByBranchId(Guid branchId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .CountAsync(branchUser =>
                branchUser.BranchId == branchId &&
                branchUser.Active &&
                branchUser.Branch.Active &&
                branchUser.Role == server.Domain.Entities.Enums.Role.Admin);
    }

    public async Task<IReadOnlyList<BranchUser>> ListActiveByBranchId(Guid branchId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .Include(branchUser => branchUser.User)
            .Where(branchUser =>
                branchUser.BranchId == branchId &&
                branchUser.Active &&
                branchUser.User.Active &&
                branchUser.Branch.Active)
            .OrderBy(branchUser => branchUser.User.Name)
            .ThenBy(branchUser => branchUser.User.Email)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<BranchUser>> ListActiveByUserId(Guid userId)
    {
        return await dbContext.BranchUsers
            .AsNoTracking()
            .Include(branchUser => branchUser.Branch)
            .Where(branchUser =>
                branchUser.UserId == userId &&
                branchUser.Active &&
                branchUser.Branch.Active)
            .OrderBy(branchUser => branchUser.Branch.Name)
            .ToListAsync();
    }
}
