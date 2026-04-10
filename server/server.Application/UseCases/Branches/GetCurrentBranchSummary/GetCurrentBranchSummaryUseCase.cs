using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Branches.GetCurrentBranchSummary;

public class GetCurrentBranchSummaryUseCase(
    IAuthenticationService authenticationService,
    IBranchesRepository branchesRepository)
{
    public async Task<ResponseGetCurrentBranchSummaryJson> Execute()
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var branch = await branchesRepository.GetById(branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.BRANCH_NOT_FOUND);

        return branch.ToCurrentBranchResponse(branchUser.Role);
    }
}
