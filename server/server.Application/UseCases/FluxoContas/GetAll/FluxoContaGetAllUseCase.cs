using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.FluxoContas.GetAll;

public class FluxoContaGetAllUseCase
{
    private readonly IFluxoContasRepository _repository;
    private readonly IAuthenticationService _authService;

    public FluxoContaGetAllUseCase(IFluxoContasRepository repository, IAuthenticationService authenticationService)
    {
        _repository = repository;
        _authService = authenticationService;
    }
    public async Task<List<ResponseFluxoContaJson>> Execute()
    {
        // var user = await _authService.GetAuthenticatedUser();
        var contas = await _repository.GetAll();
        
        return contas.Select(c => new ResponseFluxoContaJson
            {
                Id = c.Id, 
                Tipo = c.Tipo, 
                Identificação = c.Identificação, 
                Instituição = c.Instituição
            })
            .ToList();
    }
}