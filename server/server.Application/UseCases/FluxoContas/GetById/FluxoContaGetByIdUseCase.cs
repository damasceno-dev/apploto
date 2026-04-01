using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.FluxoContas.GetById;

public class FluxoContaGetByIdUseCase
{
    private readonly IFluxoContasRepository _repository;
    private readonly IAuthenticationService _authService;

    public FluxoContaGetByIdUseCase(IFluxoContasRepository repository, IAuthenticationService authenticationService)
    {
        _repository = repository;
        _authService = authenticationService;
    }
    public async Task<ResponseFluxoContaJson> Execute(Guid fluxoContaId)
    {
        // var user = await _authService.GetAuthorizedUser(Role.Admin, Role.Manager);
        var conta = await _repository.GetById(fluxoContaId);
        ValidateGetById(conta);
        return new ResponseFluxoContaJson
        {
            Id = conta!.Id,
            Identificação = conta.Identificação,
            Instituição = conta.Instituição
        };
    }
    
    private void ValidateGetById(FluxoConta? conta)
    {
        // throw new DivideByZeroException();
        if (conta is null)
        {
            throw new NotFoundException("Conta inexistente");
        }
    }
}