using server.Application.Services.Members;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.List;

public class ListDailyClosesUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseListDailyClosesJson> Execute(RequestListDailyClosesJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        var filter = request.ToFilter();

        // Members cannot filter by an arbitrary operator; Mine handles own-operator filtering.
        if (branchUser.Role == Role.Member)
        {
            filter.OperatorId = null;
        }

        // Members always need their resolved scope; non-Members only resolve when
        // Mine=true so we can surface the caller's linked operator id.
        var needsScope = branchUser.Role == Role.Member || request.Mine;
        Operator? callerOperator = null;
        IReadOnlyList<Guid> allowedAccountIds = [];
        if (needsScope)
        {
            var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
            callerOperator = memberScope.LinkedOperator;
            allowedAccountIds = memberScope.AllowedAccountIds;
        }

        // Mine convenience filter — server-resolved from the caller's linked operator.
        // No-op when the caller has no linked operator.
        if (request.Mine && callerOperator is not null)
        {
            filter.OperatorId = callerOperator.Id;
        }

        if (branchUser.Role == Role.Member)
        {
            switch (filter.AccountId)
            {
                // Empty-scope short-circuit: a Member with no linked operator OR with a
                // linked operator but zero active OperatorAccount rows AND no explicit
                // AccountId has nothing to list. Skip both repo calls.
                case null when allowedAccountIds.Count == 0:
                // Explicit AccountId outside the Member's scope → empty result without
                // hitting the repository.
                case { } explicitlySuppliedAccountId when
                    allowedAccountIds.Contains(explicitlySuppliedAccountId) is false:
                    return EmptyResponse(filter);
                default:
                    filter.AllowedAccountIds = allowedAccountIds;
                    break;
            }
        }

        // List returns only the requested page; count returns the full filtered set for pagination metadata.
        var items = await dailyClosesRepository.ListByBranchIdAsNoTracking(branchUser.BranchId, filter);
        var totalCount = await dailyClosesRepository.CountByBranchIdAsNoTracking(branchUser.BranchId, filter);

        return items.ToListResponse(filter, totalCount);
    }

    private static ResponseListDailyClosesJson EmptyResponse(DailyCloseListFilter filter)
    {
        return Array.Empty<DailyClose>().ToListResponse(filter, totalCount: 0);
    }

    private static void Validate(RequestListDailyClosesJson request)
    {
        var result = new ListDailyClosesFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
