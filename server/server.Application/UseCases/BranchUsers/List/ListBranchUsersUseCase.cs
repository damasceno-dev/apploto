using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.BranchUsers.List;

public class ListBranchUsersUseCase(
    IAuthenticationService authenticationService,
    IBranchUsersRepository branchUsersRepository)
{
    public async Task<ResponseListBranchUsersJson> Execute()
    {
        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var branchUsers = await branchUsersRepository.ListActiveByBranchId(authenticatedBranchUser.BranchId);

        return branchUsers.ToResponse();
    }
}
