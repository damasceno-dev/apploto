using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.Dashboard;

public class GetDashboardUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    IAccountsRepository accountsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    IBranchClock branchClock)
{
    public async Task<ResponseDashboardJson> Execute(RequestDashboardJson request)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var date = request.Date.Date;

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId);

        var closes = await dailyClosesRepository.ListDashboardClosesByBranchIdAndDateAsNoTracking(
            branchUser.BranchId, date);

        var varianceRows = await dailyCloseItemsRepository.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            branchUser.BranchId, cashVarianceProductId, accountId: null, dateFrom: date, dateTo: date);

        var expectedClosers = await accountsRepository.ListExpectedClosersByBranchIdAsNoTracking(branchUser.BranchId);

        // Keyed by (Date, AccountId): each account's close carries its own variance row for the day;
        // keying by date alone would collapse sibling accounts.
        var varianceByDateAndAccount = varianceRows.ToDictionary(
            r => (r.Date.Date, r.AccountId),
            r => r.Value);

        // Draft closes never surface as review rows: they belong to the not-submitted queue below,
        // carrying their close id so the UI can deep-link into the open draft.
        var closeRows = closes
            .Where(c => c.Status != DailyCloseStatus.Draft)
            .Select(c => new ResponseDashboardCloseJson
            {
                DailyCloseId = c.DailyCloseId,
                AccountId = c.AccountId,
                AccountName = c.AccountName,
                SubmittedByOperatorId = c.SubmittedByOperatorId,
                SubmittedByOperatorName = c.SubmittedByOperatorName,
                Status = c.Status,
                SubmittedAt = c.SubmittedAt,
                ApprovedAt = c.ApprovedAt,
                VarianceValue = varianceByDateAndAccount.TryGetValue((date, c.AccountId), out var variance)
                    ? variance
                    : null
            })
            .ToList();

        var submittedAccountIds = closeRows.Select(c => c.AccountId).ToHashSet();
        var draftClosesByAccountId = closes
            .Where(c => c.Status == DailyCloseStatus.Draft)
            .ToDictionary(c => c.AccountId);

        // Expected-closer rule (M7.5 item 1.2): active Terminal accounts with at least one active
        // OperatorAccount link to an active Operator are expected to close. Bank/Tab accounts are not:
        // §6.5 daily closing covers terminal cash counts and §6.4 keeps fiado balances query-time.
        // A future branch-local date has no expected closers yet — nobody is missing a close for a
        // day that has not happened at the branch.
        List<ResponseDashboardNotSubmittedJson> notSubmitted = [];
        if (date <= branchClock.LocalBusinessDate(branchClock.UtcNow()))
        {
            notSubmitted = expectedClosers
                .Where(expected => !submittedAccountIds.Contains(expected.AccountId))
                .Select(expected =>
                {
                    var draftClose = draftClosesByAccountId.GetValueOrDefault(expected.AccountId);
                    return new ResponseDashboardNotSubmittedJson
                    {
                        AccountId = expected.AccountId,
                        AccountName = expected.AccountName,
                        OperatorId = expected.OperatorId,
                        OperatorName = expected.OperatorName,
                        DailyCloseId = draftClose?.DailyCloseId,
                        Status = draftClose?.Status
                    };
                })
                .ToList();
        }

        var totalVariance = varianceRows.Sum(r => r.Value);
        var meanVariance = varianceRows.Count > 0 ? totalVariance / varianceRows.Count : 0m;

        return new ResponseDashboardJson
        {
            Date = date,
            TotalVariance = totalVariance,
            MeanVariance = meanVariance,
            PendingApprovalCount = closeRows.Count(c => c.Status == DailyCloseStatus.Submitted),
            Closes = closeRows,
            NotSubmitted = notSubmitted
        };
    }

    private static void Validate(RequestDashboardJson request)
    {
        var result = new DashboardFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
