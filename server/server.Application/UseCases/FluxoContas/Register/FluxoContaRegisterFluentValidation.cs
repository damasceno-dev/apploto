using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.FluxoContas.Register;

public class FluxoContaRegisterFluentValidation : AbstractValidator<RequestFluxoContaJson>
{
        public FluxoContaRegisterFluentValidation()
        {
            RuleFor(c => c.Identificação).NotEmpty().WithMessage("A Identificação da conta não pode ser nula ou vazia");
            RuleFor(c => c.Instituição).NotEmpty().WithMessage("A Instituição da conta não pode ser nula ou vazia");
            RuleFor(c => c.Tipo).IsInEnum().WithMessage("O Tipo da conta não está definido");
        }
}