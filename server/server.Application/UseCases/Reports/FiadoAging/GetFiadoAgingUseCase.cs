using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.FiadoAging;

public class GetFiadoAgingUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IBranchClock branchClock)
{
    public async Task<ResponseFiadoAgingJson> Execute(RequestFiadoAgingJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var asOfDate = request.AsOfDate ?? branchClock.LocalBusinessDate(branchClock.UtcNow());

        // FIFO netting is branch/client-wide across every Tab (M7.7 Phase 5.1): AccountId narrows the netted
        // remainders and never the netting input, and each item's Value is the sale's unpaid remainder.
        var items = await transactionsRepository.ListFiadoOpenSalesByBranchIdAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId, asOfDate,
            request.Page, request.PageSize);

        var totalCount = await transactionsRepository.CountFiadoOpenSalesByBranchIdAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId, asOfDate);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / request.PageSize);

        // M11 Phase 3.2: whole-set totals from the same netting projection and filters, independent of the page.
        var totals = await transactionsRepository.GetFiadoAgingTotalsByBranchIdAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId, asOfDate);

        return new ResponseFiadoAgingJson
        {
            AsOfDate = asOfDate,
            OutstandingTotal = totals.ByDueDate.Sum(row => row.Value),
            ByBucket = AgingBucketTotals.Build(totals.ByDueDate, asOfDate),
            OldestUnpaidDate = totals.OldestUnpaidDate,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = request.Page < totalPages,
            HasPrevious = request.Page > 1,
            Items = items.Select(row =>
            {
                var daysOutstanding = Math.Max(0, (asOfDate.Date - row.DueDate.Date).Days);
                var bucket = ReportAgingBucketizer.BucketFor(row.DueDate, asOfDate);
                return new ResponseFiadoAgingItemJson
                {
                    TransactionId = row.Id,
                    Date = row.Date,
                    DueDate = row.DueDate,
                    Value = row.Value,
                    DaysOutstanding = daysOutstanding,
                    Bucket = bucket,
                    ClientId = row.ClientId,
                    ClientName = row.ClientName,
                    AccountId = row.AccountId,
                    AccountName = row.AccountName,
                    Description = row.Description
                };
            }).ToList()
        };
    }

    private static void Validate(RequestFiadoAgingJson request)
    {
        var result = new FiadoAgingFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
