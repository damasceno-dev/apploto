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
    IMemberTransactionScopeResolver memberTransactionScopeResolver)
{
    public async Task<ResponseListTransactionsJson> Execute(RequestListTransactionsJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        var filter = request.ToFilter();

        if (branchUser.Role == Role.Member)
        {
            var memberScope = await memberTransactionScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
            var allowedAccountIds = memberScope.AllowedAccountIds;

            // Empty-scope short-circuit: a Member with no linked operator OR with a
            // linked operator but zero active OperatorAccount rows AND no explicit
            // AccountId has nothing to list. Skip both repo calls.
            if (filter.AccountId is null && allowedAccountIds.Count == 0)
            {
                return EmptyResponse(filter);
            }

            // Explicit AccountId outside the Member's scope → empty result without
            // hitting the repository.
            if (filter.AccountId is { } explicitlySuppliedAccountId &&
                allowedAccountIds.Contains(explicitlySuppliedAccountId) is false)
            {
                return EmptyResponse(filter);
            }

            filter.AllowedAccountIds = allowedAccountIds;
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
