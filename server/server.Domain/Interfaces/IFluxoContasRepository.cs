using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IFluxoContasRepository
{
    void Register(FluxoConta novaConta);
    Task<FluxoConta?> GetById(Guid fluxoContaId);
    Task<List<FluxoConta>> GetAll();
    Task<bool> VerifyIfExists(FluxoConta conta);
}