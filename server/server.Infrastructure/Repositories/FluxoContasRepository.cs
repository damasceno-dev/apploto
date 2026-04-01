using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class FluxoContasRepository : IFluxoContasRepository
{
    private readonly LotoDbContext _dbContext;

    public FluxoContasRepository(LotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public void Register(FluxoConta novaConta)
    {
        _dbContext.FluxoContas.Add(novaConta);
    }

    public async Task<FluxoConta?> GetById(Guid fluxoContaId)
    {
        return await _dbContext.FluxoContas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == fluxoContaId);
    }

    public async Task<List<FluxoConta>> GetAll()
    {
        return await _dbContext.FluxoContas.AsNoTracking().ToListAsync();
    }

    public async Task<bool> VerifyIfExists(FluxoConta conta)
    {
        return await _dbContext.FluxoContas.AsNoTracking().AnyAsync(c => c.Identificação == conta.Identificação && c.Instituição ==
            conta.Instituição);
    }
}