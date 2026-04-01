using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.FluxoContas.Register;

public class FluxoContaRegisterUseCase
{
    private readonly IFluxoContasRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public FluxoContaRegisterUseCase(IFluxoContasRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }
    public async Task<ResponseFluxoContaJson> Execute(RequestFluxoContaJson request)
    {
        ValidateRegister(request);
            
        var novaConta = new FluxoConta
        {
            Tipo = request.Tipo,
            Identificação = request.Identificação,
            Instituição = request.Instituição
        };

        await ValidateIfExists(novaConta);

        _repository.Register(novaConta);
        await _unitOfWork.Commit();

        return new ResponseFluxoContaJson
        {
            Id = novaConta.Id,
            Identificação = novaConta.Identificação,
            Instituição = novaConta.Instituição
        };
    }

    private async Task ValidateIfExists(FluxoConta novaConta)
    {
        if (await _repository.VerifyIfExists(novaConta))
        {
            throw new ConflictException($"A conta com instituição {novaConta.Instituição} e identificação {novaConta
                .Identificação} já existe.");
        }
    }

    private void ValidateRegister(RequestFluxoContaJson request)
    {
        // throw new DivideByZeroException();
        var validation = new FluxoContaRegisterFluentValidation();
        var result = validation.Validate(request);
        if (result.IsValid == false)
        {
            var errorMessages = result.Errors.Select(v => v.ErrorMessage).ToList();
            throw new OnValidationException(errorMessages);
        }
    }
}