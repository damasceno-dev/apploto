using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TimeEntries.Get;

public class GetTimeEntryUseCase(
    IAuthenticationService authenticationService,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    ITimeEntriesRepository timeEntriesRepository,
    ITimeEntryPoliciesRepository timeEntryPoliciesRepository,
    ITimeEntryCalculationService calculationService,
    IBranchClock branchClock)
{
    public async Task<ResponseTimeEntryJson> Execute(Guid timeEntryId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var entry = await timeEntriesRepository.GetByIdAndBranchIdAsNoTracking(timeEntryId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);

        if (branchUser.Role is Role.Member)
        {
            var scope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

            if (scope.LinkedOperator is null)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_REQUIRES_OPERATOR_LINK);

            if (entry.OperatorId != scope.LinkedOperator.Id)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
        }

        var policies = await timeEntryPoliciesRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
        var policy = TimeEntryPolicyResolver.Resolve(policies, entry.Date);

        var branchLocalNow = branchClock.LocalBusinessDateTime(branchClock.UtcNow());

        return entry.ToReadResponse(entry.Operator.Name, policy, branchLocalNow, calculationService);
    }
}
