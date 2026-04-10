using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Branches.ListMyBranches;

public class ListMyBranchesUseCase(
    IAuthenticationService authenticationService,
    IBranchUsersRepository branchUsersRepository)
{
    public async Task<ResponseListMyBranchesJson> Execute()
    {
        var authenticatedUser = await authenticationService.GetAuthenticatedUser();
        var branchUsers = await branchUsersRepository.ListActiveByUserId(authenticatedUser.Id);

        return branchUsers.ToResponse();
    }
}
