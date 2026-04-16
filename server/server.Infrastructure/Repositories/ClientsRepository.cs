using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class ClientsRepository(ServerDbContext dbContext) : IClientsRepository
{
    public async Task Add(Client client)
    {
        await dbContext.Clients.AddAsync(client);
    }

    public async Task<Client?> GetActiveByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.Clients
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.BranchId == branchId &&
                c.Active &&
                c.Branch.Active);
    }

    public async Task<Client?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == id &&
                c.BranchId == branchId &&
                c.Active &&
                c.Branch.Active);
    }

    public async Task<IReadOnlyList<Client>> ListActiveByBranchId(Guid branchId)
    {
        return await dbContext.Clients
            .AsNoTracking()
            .Where(c =>
                c.BranchId == branchId &&
                c.Active &&
                c.Branch.Active)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Client?> GetActiveByCpfAndBranchId(string cpf, Guid branchId)
    {
        return await dbContext.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Cpf == cpf &&
                c.BranchId == branchId &&
                c.Active &&
                c.Branch.Active);
    }
}