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

        var items = await transactionsRepository.ListOpenReceivablesByBranchIdAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId, asOfDate,
            request.Page, request.PageSize, AccountType.Tab);

        var totalCount = await transactionsRepository.CountOpenReceivablesByBranchIdAsNoTracking(
            branchUser.BranchId, request.AccountId, request.ClientId, asOfDate, AccountType.Tab);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / request.PageSize);

        return new ResponseFiadoAgingJson
        {
            AsOfDate = asOfDate,
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
