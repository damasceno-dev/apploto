using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.DailyLedger;

public class GetDailyLedgerUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    ITransactionsRepository transactionsRepository)
{
    public async Task<ResponseDailyLedgerJson> Execute(RequestDailyLedgerJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var account = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(
            request.AccountId, branchUser.BranchId);

        if (account is null)
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var filter = new DailyLedgerListFilter
        {
            AccountId = request.AccountId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var items = await transactionsRepository
            .ListByBranchIdAndAccountIdAndDateRangeAsNoTracking(branchUser.BranchId, filter);

        var totalCount = await transactionsRepository
            .CountByBranchIdAndAccountIdAndDateRangeAsNoTracking(branchUser.BranchId, filter);

        var openingBalance = await transactionsRepository
            .SumActiveByAccountAndDateBeforeAsNoTracking(branchUser.BranchId, request.AccountId, request.DateFrom);

        var (totalIn, totalOut) = await transactionsRepository
            .SumActiveByAccountAndDateRangeAsNoTracking(branchUser.BranchId, request.AccountId, request.DateFrom, request.DateTo);

        var netTotal = totalIn - totalOut;
        var closingBalance = openingBalance + netTotal;
        var totalPages = request.PageSize > 0
            ? (int)Math.Ceiling(totalCount / (double)request.PageSize)
            : 0;

        return new ResponseDailyLedgerJson
        {
            Items = items.Select(t => t.ToDailyLedgerItem()).ToList(),
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = request.Page < totalPages,
            HasPrevious = request.Page > 1,
            TotalIn = totalIn,
            TotalOut = totalOut,
            NetTotal = netTotal,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            AccountId = account.Id,
            AccountName = account.Name,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo
        };
    }

    private static void Validate(RequestDailyLedgerJson request)
    {
        var result = new DailyLedgerFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
