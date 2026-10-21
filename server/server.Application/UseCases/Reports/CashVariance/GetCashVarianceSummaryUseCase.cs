using server.Application.Services.DailyCloses;
using server.Application.Services.Reports;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.CashVariance;

public class GetCashVarianceSummaryUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<ResponseCashVarianceSummaryJson> Execute(
        RequestCashVarianceSummaryJson request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        if (request.AccountId is { } accountId)
        {
            var account = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(
                accountId, branchUser.BranchId, ct);
            if (account is null)
                throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        }

        var productId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);

        var rows = await dailyCloseItemsRepository.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            branchUser.BranchId, productId, request.AccountId, request.DateFrom, request.DateTo, ct);

        var figures = CashVarianceFold.From(rows);
        var totalCount = figures.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / request.PageSize);
        var pagedItems = rows
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new ResponseCashVarianceSummaryItemJson
            {
                DailyCloseId = r.DailyCloseId,
                Date = r.Date,
                AccountId = r.AccountId,
                AccountName = r.AccountName,
                VarianceValue = r.Value,
                DailyCloseStatus = r.DailyCloseStatus
            })
            .ToList();

        return new ResponseCashVarianceSummaryJson
        {
            Items = pagedItems,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasNext = request.Page < totalPages,
            HasPrevious = request.Page > 1,
            TotalVariance = figures.Total,
            MeanVariance = figures.Mean,
            MaxVariance = figures.Max,
            MinVariance = figures.Min,
            SubmittedCount = figures.SubmittedCount,
            ApprovedCount = figures.ApprovedCount,
            RejectedCount = figures.RejectedCount
        };
    }

    private static void Validate(RequestCashVarianceSummaryJson request)
    {
        var result = new CashVarianceSummaryFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
