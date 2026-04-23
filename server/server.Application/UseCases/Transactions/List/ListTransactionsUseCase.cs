using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.List;

public class ListTransactionsUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository)
{
    public async Task<ResponseListTransactionsJson> Execute(RequestListTransactionsJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        Validate(request);

        var filter = request.ToFilter();

        if (branchUser.Role == Role.Member)
        {
            var callerOperator = await operatorsRepository
                .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId);

            var allowedAccountIds = callerOperator is null
                ? []
                : (await operatorAccountsRepository.ListActiveByOperatorIdAsNoTracking(callerOperator.Id))
                    .Select(link => link.AccountId)
                    .ToList();

            if (filter.AccountId is { } explicitlySuppliedAccountId &&
                allowedAccountIds.Contains(explicitlySuppliedAccountId) is false)
            {
                return new ResponseListTransactionsJson
                {
                    Items = [],
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalCount = 0
                };
            }

            filter.AllowedAccountIds = allowedAccountIds;
        }

        var items = await transactionsRepository.ListByBranchIdAsNoTracking(branchUser.BranchId, filter);
        var totalCount = await transactionsRepository.CountByBranchIdAsNoTracking(branchUser.BranchId, filter);

        return items.ToListResponse(filter, totalCount);
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