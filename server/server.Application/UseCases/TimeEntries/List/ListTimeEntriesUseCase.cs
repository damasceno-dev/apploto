using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TimeEntries.List;

public class ListTimeEntriesUseCase(
    IAuthenticationService authenticationService,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    ITimeEntriesRepository timeEntriesRepository,
    ITimeEntryPoliciesRepository timeEntryPoliciesRepository,
    ITimeEntryCalculationService calculationService,
    IBranchClock branchClock)
{
    public async Task<ResponseListTimeEntriesJson> Execute(RequestListTimeEntriesJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        var filter = new TimeEntryListFilter
        {
            OperatorId = request.OperatorId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Status = request.Status,
            IsInProgress = request.IsInProgress,
            InProgressFirst = request.InProgressFirst,
            Mine = request.Mine,
            Page = request.Page,
            PageSize = request.PageSize
        };

        if (branchUser.Role is Role.Member)
        {
            var scope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);

            // Empty-scope short-circuit: a Member with no linked operator has nothing to list,
            // regardless of Mine or OperatorId. Skip both repository calls.
            if (scope.LinkedOperator is null)
                return EmptyResponse(filter);

            // Members cannot read other operators' entries. Reject an explicit OperatorId
            // that targets someone other than the caller; force the filter to the caller's
            // linked operator id regardless of what the request asked for.
            if (request.OperatorId is { } explicitOperatorId && explicitOperatorId != scope.LinkedOperator.Id)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);

            filter.OperatorId = scope.LinkedOperator.Id;
        }
        else if (request.Mine is true)
        {
            // Manager / Admin using Mine: resolve their own linked operator if any; otherwise
            // the filter naturally returns no rows (no linked operator means no own rows).
            var scope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
            if (scope.LinkedOperator is null)
                return EmptyResponse(filter);

            filter.OperatorId = scope.LinkedOperator.Id;
        }

        var items = await timeEntriesRepository.ListByBranchIdAsNoTracking(branchUser.BranchId, filter);
        var totalCount = await timeEntriesRepository.CountByBranchIdAsNoTracking(branchUser.BranchId, filter);

        var policies = await timeEntryPoliciesRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
        var branchLocalNow = branchClock.LocalBusinessDateTime(branchClock.UtcNow());

        return BuildResponse(items, totalCount, filter, policies, branchLocalNow);
    }

    private ResponseListTimeEntriesJson BuildResponse(
        IReadOnlyList<TimeEntry> items,
        int totalCount,
        TimeEntryListFilter filter,
        IReadOnlyList<TimeEntryPolicy> policies,
        DateTime branchLocalNow)
    {
        var totalPages = filter.PageSize > 0
            ? (int)Math.Ceiling(totalCount / (double)filter.PageSize)
            : 0;

        return new ResponseListTimeEntriesJson
        {
            // Per-row policy resolution: a listed page can span an effective-date boundary,
            // so each entry recomputes under the policy applicable to its own date.
            Items = items
                .Select(item => item.ToListItemResponse(
                    item.Operator.Name,
                    TimeEntryPolicyResolver.Resolve(policies, item.Date),
                    branchLocalNow,
                    calculationService))
                .ToList(),
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = filter.Page < totalPages,
            HasPrevious = filter.Page > 1,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    private static ResponseListTimeEntriesJson EmptyResponse(TimeEntryListFilter filter)
    {
        return new ResponseListTimeEntriesJson
        {
            Items = [],
            TotalCount = 0,
            TotalPages = 0,
            HasNext = false,
            HasPrevious = false,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    private static void Validate(RequestListTimeEntriesJson request)
    {
        var result = new ListTimeEntriesFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
