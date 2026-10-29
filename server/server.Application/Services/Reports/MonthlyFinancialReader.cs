using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace server.Application.Services.Reports;

/// <summary>
/// The monthly financial report's totals, folded in the database from the same predicates as the detailed report
/// (M11 Phase 7 review): what the management overview shows without materializing days, leftovers or closes.
/// </summary>
public sealed record MonthlyFinancialTotals(
    DateTime DateFrom,
    DateTime DateTo,
    bool IsPartialMonth,
    decimal OfficialVarianceTotal,
    int OfficialCloseCount,
    int CloseCount,
    bool IsBolaoServiceFeeConfigured,
    decimal? BolaoServiceFeeTotal,
    int BolaoServiceFeeRowCount,
    bool IsCommercialLeftoverConfigured,
    decimal? CommercialLeftoverTotal,
    int CommercialLeftoverRowCount,
    decimal HoursTotal,
    int HourDayCount,
    bool ContainsInProgress);

/// <summary>
/// The one monthly financial computation (M11 Phase 6), shared by the full report and the management overview.
/// Callers own authorization and validation, including refusing a month that has not started.
/// </summary>
public class MonthlyFinancialReader(
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    ITransactionsRepository transactionsRepository,
    ITransactionTypesRepository transactionTypesRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    TimeEntryBalanceProjector timeEntryBalanceProjector)
{
    public async Task<ResponseMonthlyFinancialReportJson> Read(
        Guid branchId,
        int year,
        int month,
        DateTime asOfDate,
        DateTime branchLocalNow,
        CancellationToken ct = default,
        IReadOnlyList<ResponseTimeEntryBalanceOperatorJson>? hoursRollUp = null)
    {
        // The overview passes the roll-up it already computed for this same window and instant.
        var (dateFrom, dateTo, _) = MonthWindow(year, month, asOfDate);
        var operators = hoursRollUp ?? await timeEntryBalanceProjector.BuildBranchWide(branchId, dateFrom, dateTo, branchLocalNow, ct);
        var totals = await ReadTotals(branchId, year, month, asOfDate, operators, ct);
        var feeConfigured = totals.IsBolaoServiceFeeConfigured;
        var leftoverConfigured = totals.IsCommercialLeftoverConfigured;

        var productId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);
        var varianceRows = await dailyCloseItemsRepository.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            branchId, productId, accountId: null, dateFrom, dateTo, ct);
        var closes = await dailyClosesRepository.ListCloseSummariesByBranchIdAndDateRangeAsNoTracking(
            branchId, dateFrom, dateTo, take: null, ct);

        IReadOnlyList<MonthlyFinancialRoleRow> feeRows = feeConfigured
            ? await transactionsRepository.ListMonthlyFinancialRoleRowsByBranchIdAsNoTracking(
                branchId, MonthlyFinancialRole.BolaoServiceFee, dateFrom, dateTo, ct)
            : [];
        IReadOnlyList<MonthlyFinancialRoleRow> leftoverRows = leftoverConfigured
            ? await transactionsRepository.ListMonthlyFinancialRoleRowsByBranchIdAsNoTracking(
                branchId, MonthlyFinancialRole.CommercialLeftover, dateFrom, dateTo, ct)
            : [];


        // Official variance is keyed by close id: a Draft close never has a row here, so its retained value is suppressed.
        var varianceByClose = varianceRows.ToDictionary(row => row.DailyCloseId, row => row.Value);
        // Fees attach to their own session (a shared cashbox has several per day); unresolved legacy rows carry no
        // session and stay keyed to their account/day.
        var feeByTerminalDay = feeRows
            .GroupBy(row => (row.Date.Date, row.AccountId, row.SessionId))
            .ToDictionary(group => group.Key, group => (
                Value: group.Sum(row => row.InTotal - row.OutTotal),
                RowCount: group.Sum(row => row.RowCount),
                group.First().AccountName,
                group.First().AccountActive));

        (decimal Value, int RowCount, string AccountName, bool AccountActive) FeeFor(DailyCloseSummaryRow close) =>
            feeByTerminalDay.TryGetValue((close.Date.Date, close.AccountId, close.DailyCloseId), out var own)
                ? own
                : feeByTerminalDay.GetValueOrDefault((close.Date.Date, close.AccountId, null));

        var terminalDays = closes
            .Select(close => new ResponseMonthlyFinancialTerminalDayJson
            {
                Date = close.Date.Date,
                AccountId = close.AccountId,
                AccountName = close.AccountName,
                AccountActive = close.AccountActive,
                DailyCloseId = close.DailyCloseId,
                CloseStatus = close.Status,
                OfficialVariance = varianceByClose.TryGetValue(close.DailyCloseId, out var variance) ? variance : null,
                BolaoServiceFee = feeConfigured ? FeeFor(close).Value : null,
                BolaoServiceFeeRowCount = FeeFor(close).RowCount,
                RecordedByUserId = close.RecordedByUserId,
                RecordedByUserName = close.RecordedByUserName,
                RecordedByOperatorId = close.RecordedByOperatorId,
                RecordedByOperatorName = close.RecordedByOperatorName,
                SubmittedByUserId = close.SubmittedByUserId,
                SubmittedByUserName = close.SubmittedByUserName,
                SubmittedByOperatorId = close.SubmittedByOperatorId,
                SubmittedByOperatorName = close.SubmittedByOperatorName
            })
            .Concat(feeByTerminalDay
                .Where(pair => closes.All(close =>
                    (close.Date.Date, close.AccountId) != (pair.Key.Date, pair.Key.AccountId)
                    || (pair.Key.SessionId is { } sessionId && sessionId != close.DailyCloseId)))
                .Select(pair => new ResponseMonthlyFinancialTerminalDayJson
                {
                    Date = pair.Key.Date,
                    AccountId = pair.Key.AccountId,
                    AccountName = pair.Value.AccountName,
                    AccountActive = pair.Value.AccountActive,
                    BolaoServiceFee = pair.Value.Value,
                    BolaoServiceFeeRowCount = pair.Value.RowCount
                }))
            .OrderBy(day => day.Date)
            .ThenBy(day => day.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(day => day.AccountId)
            .ThenBy(day => day.DailyCloseId)
            .ToList();

        var leftovers = leftoverRows
            .Select(row => new ResponseMonthlyFinancialLeftoverJson
            {
                Date = row.Date.Date,
                AccountId = row.AccountId,
                AccountName = row.AccountName,
                AccountActive = row.AccountActive,
                RecordedByOperatorId = row.RecordedByOperatorId,
                RecordedByOperatorName = row.RecordedByOperatorName,
                RecordedByOperatorActive = row.RecordedByOperatorActive,
                TransactionTypeId = row.TransactionTypeId,
                TransactionTypeName = row.TransactionTypeName,
                Value = row.OutTotal - row.InTotal,
                RowCount = row.RowCount
            })
            .OrderBy(row => row.Date)
            .ThenBy(row => row.AccountName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.AccountId)
            .ThenBy(row => row.RecordedByOperatorName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RecordedByOperatorId)
            .ThenBy(row => row.TransactionTypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.TransactionTypeId)
            .ToList();

        var leftoversByType = leftoverRows
            .GroupBy(row => row.TransactionTypeId)
            .Select(group => new ResponseMonthlyFinancialLeftoverTypeTotalJson
            {
                TransactionTypeId = group.Key,
                TransactionTypeName = group.First().TransactionTypeName,
                TransactionTypeActive = group.First().TransactionTypeActive,
                Total = group.Sum(row => row.OutTotal - row.InTotal),
                RowCount = group.Sum(row => row.RowCount)
            })
            .OrderBy(total => total.TransactionTypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(total => total.TransactionTypeId)
            .ToList();

        var hours = operators
            .SelectMany(op => op.Days.Select(day => new ResponseMonthlyFinancialHoursDayJson
            {
                Date = day.Date.Date,
                OperatorId = op.OperatorId,
                OperatorName = op.OperatorName,
                OperatorActive = op.OperatorActive,
                Status = day.Status,
                TotalHours = day.TotalHours,
                BalanceHours = day.BalanceHours,
                IsInProgress = day.IsInProgress
            }))
            .OrderBy(day => day.Date)
            .ThenBy(day => day.OperatorName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(day => day.OperatorId)
            .ToList();

        return new ResponseMonthlyFinancialReportJson
        {
            Year = year,
            Month = month,
            AsOfDate = asOfDate.Date,
            BranchLocalNow = branchLocalNow,
            DateFrom = totals.DateFrom,
            DateTo = totals.DateTo,
            IsPartialMonth = totals.IsPartialMonth,
            OfficialVarianceTotal = totals.OfficialVarianceTotal,
            OfficialCloseCount = totals.OfficialCloseCount,
            IsBolaoServiceFeeConfigured = feeConfigured,
            BolaoServiceFeeTotal = totals.BolaoServiceFeeTotal,
            IsCommercialLeftoverConfigured = leftoverConfigured,
            CommercialLeftoverTotal = totals.CommercialLeftoverTotal,
            LeftoversByType = leftoversByType,
            HoursTotal = totals.HoursTotal,
            ContainsInProgress = totals.ContainsInProgress,
            TerminalDays = terminalDays,
            Leftovers = leftovers,
            Hours = hours
        };
    }

    /// <summary>
    /// The month's totals from database aggregates over the report's own predicates: official variance per status,
    /// the close count, each role's signed sum (fee = In − Out, leftover = Out − In) and the hours of the given roll-up.
    /// </summary>
    public async Task<MonthlyFinancialTotals> ReadTotals(
        Guid branchId,
        int year,
        int month,
        DateTime asOfDate,
        IReadOnlyList<ResponseTimeEntryBalanceOperatorJson> hoursRollUp,
        CancellationToken ct = default)
    {
        var (dateFrom, dateTo, isPartialMonth) = MonthWindow(year, month, asOfDate);

        var roles = await transactionTypesRepository.ListConfiguredMonthlyFinancialRolesByBranchIdAsNoTracking(branchId, ct);
        var feeConfigured = roles.Contains(MonthlyFinancialRole.BolaoServiceFee);
        var leftoverConfigured = roles.Contains(MonthlyFinancialRole.CommercialLeftover);

        var productId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);
        var variance = CashVarianceFold.FromStatusTotals(
            await dailyCloseItemsRepository.ListOfficialVarianceStatusTotalsAsNoTracking(branchId, productId, null, dateFrom, dateTo, ct));
        var closeCount = (await dailyClosesRepository.CountByStatusByBranchIdAndDateRangeAsNoTracking(branchId, dateFrom, dateTo, ct))
            .Sum(row => row.Count);
        var fee = feeConfigured
            ? await transactionsRepository.GetMonthlyFinancialRoleTotalAsNoTracking(
                branchId, MonthlyFinancialRole.BolaoServiceFee, dateFrom, dateTo, ct)
            : null;
        var leftover = leftoverConfigured
            ? await transactionsRepository.GetMonthlyFinancialRoleTotalAsNoTracking(
                branchId, MonthlyFinancialRole.CommercialLeftover, dateFrom, dateTo, ct)
            : null;

        return new MonthlyFinancialTotals(
            dateFrom,
            dateTo,
            isPartialMonth,
            variance.Total,
            variance.Count,
            closeCount,
            feeConfigured,
            fee is null ? null : fee.InTotal - fee.OutTotal,
            fee?.RowCount ?? 0,
            leftoverConfigured,
            leftover is null ? null : leftover.OutTotal - leftover.InTotal,
            leftover?.RowCount ?? 0,
            hoursRollUp.Sum(op => op.TotalHours),
            hoursRollUp.Sum(op => op.Days.Count),
            hoursRollUp.Any(op => op.ContainsInProgress));
    }

    /// <summary>
    /// The month's report window: the whole calendar month once it has ended, else its first day through
    /// <paramref name="asOfDate"/>, flagged partial — on the month's last day too, which has not ended yet. A month
    /// starting after <paramref name="asOfDate"/> is the caller's error.
    /// </summary>
    public static (DateTime DateFrom, DateTime DateTo, bool IsPartialMonth) MonthWindow(int year, int month, DateTime asOfDate)
    {
        var dateFrom = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var monthEnd = dateFrom.AddMonths(1).AddDays(-1);
        var isPartialMonth = monthEnd >= asOfDate.Date;
        return (dateFrom, isPartialMonth ? asOfDate.Date : monthEnd, isPartialMonth);
    }

    public static bool IsFutureMonth(int year, int month, DateTime asOfDate)
    {
        return new DateTime(year, month, 1) > asOfDate.Date;
    }
}
