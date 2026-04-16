using FluentValidation;
using server.Application.UseCases.Clients;
using server.Communication.Requests;

namespace server.Application.UseCases.Clients.Update;

public class UpdateClientFluentValidation : AbstractValidator<RequestUpdateClientJson>
{
    public UpdateClientFluentValidation()
    {
        RuleFor(r => r.Name).ValidateClientName();
        RuleFor(r => r.Phone).ValidateClientPhone();
        RuleFor(r => r.Cpf)
            .ValidClientCpf()
            .When(r => string.IsNullOrWhiteSpace(r.Cpf) is false);
        RuleFor(r => r.Email)
            .ValidClientEmail()
            .When(r => string.IsNullOrWhiteSpace(r.Email) is false);
    }
}
