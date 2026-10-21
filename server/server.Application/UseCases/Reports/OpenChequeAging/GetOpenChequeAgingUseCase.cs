using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.OpenChequeAging;

public class GetOpenChequeAgingUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IBranchClock branchClock)
{
    public async Task<ResponseOpenChequeAgingJson> Execute(RequestOpenChequeAgingJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var asOfDate = request.AsOfDate ?? branchClock.LocalBusinessDate(branchClock.UtcNow());

        var groups = await transactionsRepository.ListOpenChequeGroupsByBranchIdAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId, request.Page, request.PageSize);

        var totalCount = await transactionsRepository.CountOpenChequeGroupsByBranchIdAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / request.PageSize);

        // M11 Phase 2.1: whole-filter totals from one grouped query, independent of the page.
        var dueDateTotals = await transactionsRepository.ListOpenChequeTotalsByDueDateAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId);

        var groupResponses = groups
            .Where(group => group.OpenRows.Count > 0)
            .Select(group => new ResponseOpenChequeAgingGroupJson
            {
                OriginTransactionId = group.OriginTransactionId,
                OutstandingTotal = group.OutstandingTotal,
                OldestOpenDueDate = group.OldestOpenDueDate,
                OldestOpenBucket = ReportAgingBucketizer.BucketFor(group.OldestOpenDueDate, asOfDate),
                OpenRowCount = group.OpenRowCount,
                TotalRowCount = group.TotalRowCount,
                ClientId = group.ClientId,
                ClientName = group.ClientName,
                AccountId = group.AccountId,
                AccountName = group.AccountName,
                Description = group.Description,
                Rows = group.OpenRows
                    .Select(row => new ResponseOpenChequeAgingRowJson
                    {
                        TransactionId = row.TransactionId,
                        DueDate = row.DueDate,
                        Value = row.Value,
                        DaysOutstanding = Math.Max(0, (asOfDate.Date - row.DueDate.Date).Days),
                        Bucket = ReportAgingBucketizer.BucketFor(row.DueDate, asOfDate)
                    })
                    .ToList()
            })
            .ToList();

        return new ResponseOpenChequeAgingJson
        {
            Items = groupResponses,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = request.Page < totalPages,
            HasPrevious = request.Page > 1,
            AsOfDate = asOfDate,
            OutstandingTotal = dueDateTotals.Sum(row => row.Value),
            OpenRowCount = dueDateTotals.Sum(row => row.RowCount),
            ByBucket = AgingBucketTotals.Build(dueDateTotals, asOfDate)
        };
    }

    private static void Validate(RequestOpenChequeAgingJson request)
    {
        var result = new OpenChequeAgingFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
