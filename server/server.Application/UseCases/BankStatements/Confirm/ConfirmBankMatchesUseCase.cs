using server.Application.Services.Banking;
using server.Communication.Requests;
using server.Communication.Responses;

namespace server.Application.UseCases.BankStatements.Confirm;

public class ConfirmBankMatchesUseCase(BankMatchService service)
{
    public Task<ResponseBankMatchConfirmationJson> Execute(RequestBankMatchConfirmJson request, string? key, CancellationToken ct = default) => service.Confirm(request, key, ct);
}
