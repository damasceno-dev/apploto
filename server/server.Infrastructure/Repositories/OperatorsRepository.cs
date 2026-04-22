using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class OperatorsRepository(ServerDbContext dbContext) : IOperatorsRepository
{
    public async Task Add(Operator op)
    {
        await dbContext.Operators.AddAsync(op);
    }

    public async Task<Operator?> GetActiveByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.Operators
            .FirstOrDefaultAsync(op =>
                op.Id == id &&
                op.BranchId == branchId &&
                op.Active &&
                op.Branch.Active);
    }

    public async Task<Operator?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.Operators
            .AsNoTracking()
            .FirstOrDefaultAsync(op =>
                op.Id == id &&
                op.BranchId == branchId &&
                op.Active &&
                op.Branch.Active);
    }

    public async Task<IReadOnlyList<Operator>> ListActiveByBranchId(Guid branchId)
    {
        return await dbContext.Operators
            .AsNoTracking()
            .Where(op =>
                op.BranchId == branchId &&
                op.Active &&
                op.Branch.Active)
            .OrderBy(op => op.Name)
            .ToListAsync();
    }

    public async Task<Operator?> GetActiveLinkedByUserIdAndBranchIdAsNoTracking(Guid userId, Guid branchId)
    {
        return await dbContext.Operators
            .AsNoTracking()
            .Where(op =>
                op.UserId == userId &&
                op.BranchId == branchId &&
                op.Active &&
                op.Branch.Active)
            .OrderBy(op => op.Id)
            .SingleOrDefaultAsync();
    }

    public async Task<bool> ExistsActiveLinkedByUserIdAndBranchId(
        Guid userId,
        Guid branchId,
        Guid? exceptOperatorId = null)
    {
        return await dbContext.Operators
            .AsNoTracking()
            .AnyAsync(op =>
                op.UserId == userId &&
                op.BranchId == branchId &&
                op.Active &&
                op.Branch.Active &&
                (exceptOperatorId == null || op.Id != exceptOperatorId.Value));
    }
}
