using server.Application.Services.Banking;
using server.Communication.Requests;
using server.Communication.Responses;

namespace server.Application.UseCases.BankStatements.Propose;

public class ProposeBankMatchesUseCase(BankMatchService service)
{
    public Task<ResponseBankMatchProposalsJson> Execute(RequestBankMatchProposeJson request, string? key, CancellationToken ct = default) => service.Propose(request, key, ct);
}
