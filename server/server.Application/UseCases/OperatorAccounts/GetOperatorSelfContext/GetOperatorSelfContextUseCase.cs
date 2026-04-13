using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.OperatorAccounts.GetOperatorSelfContext;

public class GetOperatorSelfContextUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository)
{
    public async Task<ResponseSelfContextJson> Execute()
    {
        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var op = await operatorsRepository.GetActiveByUserIdAndBranchId(
            authenticatedBranchUser.UserId,
            authenticatedBranchUser.BranchId);

        if (op is null)
            return GetOperatorSelfContextMapper.ToSelfContextResponse(null, []);

        var links = await operatorAccountsRepository.ListActiveByOperatorIdWithAccount(op.Id);

        return GetOperatorSelfContextMapper.ToSelfContextResponse(op, links);
    }
}
