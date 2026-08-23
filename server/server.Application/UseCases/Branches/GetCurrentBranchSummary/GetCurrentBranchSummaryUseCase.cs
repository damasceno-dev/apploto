using server.Communication.Responses;
using server.Application.Services.Transactions;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Branches.GetCurrentBranchSummary;

public class GetCurrentBranchSummaryUseCase(
    IAuthenticationService authenticationService,
    IBranchesRepository branchesRepository,
    IBranchClock branchClock)
{
    public async Task<ResponseGetCurrentBranchSummaryJson> Execute()
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var branch = await branchesRepository.GetById(branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.BRANCH_NOT_FOUND);

        var utcNow = branchClock.UtcNow();
        var branchLocalDateTime = branchClock.LocalBusinessDateTime(utcNow);

        return branch.ToCurrentBranchResponse(
            branchUser.Role,
            DateOnly.FromDateTime(branchLocalDateTime),
            branchLocalDateTime);
    }
}
