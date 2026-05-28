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

        // N+1 acceptable: typical branch has < 20 open cheque groups per page.
        var groupResponses = new List<ResponseOpenChequeAgingGroupJson>();
        foreach (var group in groups)
        {
            var siblings = await transactionsRepository.ListActiveByOriginTransactionIdAndBranchIdAsNoTracking(
                group.OriginTransactionId, branchUser.BranchId);

            var unpaidSiblings = siblings.Where(s => s.PaidAt == null).ToList();

            // Defensive: repo should already exclude fully-paid groups; skip if none unpaid.
            if (unpaidSiblings.Count == 0) continue;

            var rows = unpaidSiblings.Select(s =>
            {
                var daysOutstanding = Math.Max(0, (asOfDate.Date - s.DueDate.Date).Days);
                var bucket = ReportAgingBucketizer.BucketFor(s.DueDate, asOfDate);
                return new ResponseOpenChequeAgingRowJson
                {
                    TransactionId = s.Id,
                    DueDate = s.DueDate,
                    Value = s.Value,
                    DaysOutstanding = daysOutstanding,
                    Bucket = bucket
                };
            }).ToList();

            var oldestOpenBucket = ReportAgingBucketizer.BucketFor(group.OldestOpenDueDate, asOfDate);

            groupResponses.Add(new ResponseOpenChequeAgingGroupJson
            {
                OriginTransactionId = group.OriginTransactionId,
                OutstandingTotal = group.OutstandingTotal,
                OldestOpenDueDate = group.OldestOpenDueDate,
                OldestOpenBucket = oldestOpenBucket,
                OpenRowCount = group.OpenRowCount,
                TotalRowCount = group.TotalRowCount,
                ClientId = group.ClientId,
                ClientName = group.ClientName,
                AccountId = group.AccountId,
                AccountName = group.AccountName,
                Description = group.Description,
                Rows = rows
            });
        }

        return new ResponseOpenChequeAgingJson
        {
            Items = groupResponses,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = request.Page < totalPages,
            HasPrevious = request.Page > 1,
            AsOfDate = asOfDate
        };
    }

    private static void Validate(RequestOpenChequeAgingJson request)
    {
        var result = new OpenChequeAgingFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
