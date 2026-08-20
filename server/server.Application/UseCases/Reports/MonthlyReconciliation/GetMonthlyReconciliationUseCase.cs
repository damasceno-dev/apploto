using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.MonthlyReconciliation;

public class GetMonthlyReconciliationUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    ITransactionsRepository transactionsRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    MonthLockReadinessEvaluator readinessEvaluator)
{
    public async Task<ResponseMonthlyReconciliationJson> Execute(
        RequestMonthlyReconciliationJson request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var year = request.Year;
        var month = request.Month;

        var dateFrom = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var dateTo = dateFrom.AddMonths(1).AddDays(-1);

        var closes = await dailyClosesRepository.ListByBranchIdAndYearMonthAsNoTracking(
            branchUser.BranchId, year, month, ct);

        // Cash variance ("Diferença Caixa") is the counted-minus-expected cash difference recorded as a
        // DailyCloseItem line; resolve that product's id for the branch before reading its values.
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);

        // One row per (close, account) carrying that close's cash-variance value across the month window.
        var cashVarianceRows = await dailyCloseItemsRepository.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            branchUser.BranchId, cashVarianceProductId, accountId: null, dateFrom, dateTo, ct);

        // Per-(Date, Status) transaction counts for the branch/month, spanning Active/Draft/Cancelled.
        var statusCounts = await transactionsRepository.CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
            branchUser.BranchId, year, month, ct);
        var directTerminalActivityPairs = await transactionsRepository
            .ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
                branchUser.BranchId, year, month, ct);
        var readiness = readinessEvaluator.Evaluate(closes, statusCounts, directTerminalActivityPairs);

        // Each account's OWN cash variance for a day. Keyed by (Date, AccountId) because one day can have
        // several closes — one per account/terminal — each with its own value. Keying by Date alone would
        // collapse them (e.g. account 1 = +10 and account 2 = +5 would lose the per-account split).
        var cashVarianceByDateAndAccount = cashVarianceRows.ToDictionary(
            r => (r.Date.Date, r.AccountId),
            r => r.Value);

        // The day's TOTAL cash variance across all of its accounts (e.g., +10 and +5 on the same day => +15),
        // used for the per-day NetVariance headline.
        var netVarianceByDate = cashVarianceRows
            .GroupBy(r => r.Date.Date)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Value));

        var countsByDateAndStatus = statusCounts.ToDictionary(
            r => (r.Date.Date, r.Status),
            r => r.Count);

        var days = Enumerable.Range(1, DateTime.DaysInMonth(year, month))
            .Select(day =>
            {
                var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
                var dateOnly = date.Date;
                var dayCloses = closes
                    .Where(c => c.Date.Date == dateOnly)
                    .Select(c => new ResponseMonthlyReconciliationDayCloseJson
                    {
                        DailyCloseId = c.Id,
                        AccountId = c.AccountId,
                        AccountName = c.Account.Name,
                        Status = c.Status,
                        VarianceValue = cashVarianceByDateAndAccount.GetValueOrDefault((dateOnly, c.AccountId), 0m)
                    })
                    .ToList();

                return new ResponseMonthlyReconciliationDayJson
                {
                    Date = date,
                    Closes = dayCloses,
                    ActiveTransactionCount = countsByDateAndStatus.GetValueOrDefault((dateOnly, TransactionStatus.Active), 0),
                    DraftTransactionCount = countsByDateAndStatus.GetValueOrDefault((dateOnly, TransactionStatus.Draft), 0),
                    CancelledTransactionCount = countsByDateAndStatus.GetValueOrDefault((dateOnly, TransactionStatus.Cancelled), 0),
                    NetVariance = netVarianceByDate.GetValueOrDefault(dateOnly, 0m)
                };
            })
            .ToList();

        // Structured blockers: each non-Approved close, then each day with outstanding Draft transactions.
        // Clients format and localize the display text themselves from these fields.
        var blockers = readiness.UnapprovedCloses
            .Select(c => new ResponseMonthlyReconciliationBlockerJson
            {
                Type = MonthlyReconciliationBlockerType.UnapprovedClose,
                Day = c.Date.Day,
                DailyCloseId = c.Id,
                AccountId = c.AccountId,
                AccountName = c.Account.Name,
                CloseStatus = c.Status
            })
            .Concat(statusCounts
                .Where(r => r is { Status: TransactionStatus.Draft, Count: > 0 })
                .Select(r => new ResponseMonthlyReconciliationBlockerJson
                {
                    Type = MonthlyReconciliationBlockerType.DraftTransactions,
                    Day = r.Date.Day,
                    DraftTransactionCount = r.Count
                }))
            .Concat(readiness.MissingExpectedCloses
                .Select(activity => new ResponseMonthlyReconciliationBlockerJson
                {
                    Type = MonthlyReconciliationBlockerType.MissingExpectedClose,
                    Day = activity.Date.Day,
                    AccountId = activity.AccountId,
                    AccountName = activity.AccountName
                }))
            .ToList();

        return new ResponseMonthlyReconciliationJson
        {
            Year = year,
            Month = month,
            LockReady = readiness.IsReady,
            Days = days,
            Blockers = blockers
        };
    }

    private static void Validate(RequestMonthlyReconciliationJson request)
    {
        var result = new MonthlyReconciliationFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
