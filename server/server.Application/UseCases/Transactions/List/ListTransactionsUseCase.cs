using server.Application.Services.Members;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.List;

public class ListTransactionsUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver)
{
    public async Task<ResponseListTransactionsJson> Execute(RequestListTransactionsJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        var filter = request.ToFilter();

        // 5.5.15: Member list scope is account-based; an arbitrary client-supplied
        // OperatorId is meaningless for Members. Use Mine for own-operator filtering.
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

        // 5.5.16: Mine convenience filter — server-resolved from the caller's linked
        // operator. No-op when the caller has no linked operator.
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

        var items = await transactionsRepository.ListByBranchIdAsNoTracking(branchUser.BranchId, filter);
        var totalCount = await transactionsRepository.CountByBranchIdAsNoTracking(branchUser.BranchId, filter);

        return items.ToListResponse(filter, totalCount);
    }

    private static ResponseListTransactionsJson EmptyResponse(TransactionListFilter filter)
    {
        return Array.Empty<Transaction>().ToListResponse(filter, totalCount: 0);
    }

    private static void Validate(RequestListTransactionsJson request)
    {
        var result = new ListTransactionsFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
