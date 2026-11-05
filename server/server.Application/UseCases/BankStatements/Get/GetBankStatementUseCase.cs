using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BankStatements.Get;

public class GetBankStatementUseCase(IAuthenticationService authentication, IBankStatementsRepository statements, BankStatementResponseBuilder responseBuilder)
{
    public async Task<ResponseBankStatementJson> Execute(Guid id, CancellationToken ct = default)
    {
        var actor = await authentication.GetAuthorizedBranchUser(Role.Manager, Role.Admin);
        var import = await statements.GetImportAsNoTracking(id, actor.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
        return await responseBuilder.Build(import, false, ct);
    }

}
