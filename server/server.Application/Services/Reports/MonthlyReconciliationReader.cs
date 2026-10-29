using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace server.Application.Services.Reports;

/// <summary>
/// The month's lock context and blocker counts from the evaluator shared with <c>POST /setting/lock-month</c>, without
/// the per-day variance and detail the full report adds (M11 Phase 7 review: the overview's reconciliation section).
/// </summary>
public sealed record MonthlyLockContext(
    bool LockReady,
    DateTime? LockDate,
    bool IsMonthLocked,
    bool IsMonthElapsed,
    int UnapprovedCloseBlockerCount,
    int DraftTransactionBlockerCount,
    int MissingExpectedCloseBlockerCount,
    bool HasRecordedActivity);

/// <summary>
/// The single-month reconciliation computation (M7 Phase 10, M11 Phase 2.3), extracted unchanged in M11 Phase 7.4 so
/// <c>GET /report/monthly-reconciliation/{year}/{month}</c> and the management overview read one rule — including the
/// lock evaluator shared with <c>POST /setting/lock-month</c>. No authorization here.
/// </summary>
public class MonthlyReconciliationReader(
    IDailyClosesRepository dailyClosesRepository,
    ITransactionsRepository transactionsRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    MonthLockReadinessEvaluator readinessEvaluator,
    ILockDateReader lockDateReader)
{
    public async Task<ResponseMonthlyReconciliationJson> Read(
        Guid branchId,
        int year,
        int month,
        DateTime branchLocalToday,
        CancellationToken ct = default)
    {
        var dateFrom = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var dateTo = dateFrom.AddMonths(1).AddDays(-1);

        var (closes, statusCounts, readiness) = await Evaluate(branchId, year, month, ct);

        // Cash variance ("Diferença Caixa") is the counted-minus-expected cash difference recorded as a
        // DailyCloseItem line; resolve that product's id for the branch before reading its values.
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);

        // One row per (close, account) carrying that close's cash-variance value across the month window.
        var cashVarianceRows = await dailyCloseItemsRepository.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            branchId, cashVarianceProductId, accountId: null, dateFrom, dateTo, ct);

        // Each session's OWN cash variance. Keyed by session id: one day has several sessions — one per cashbox and
        // possibly several on a shared cashbox — each with its own official value, never added across sessions.
        var cashVarianceBySession = cashVarianceRows.ToDictionary(r => r.DailyCloseId, r => r.Value);

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
                        VarianceValue = cashVarianceBySession.GetValueOrDefault(c.Id, 0m)
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

        // M11 Phase 2.3 lock context; LockReady and the blockers above are unchanged.
        var lockDate = await lockDateReader.Read(branchId, ct);
        var today = branchLocalToday.Date;

        return new ResponseMonthlyReconciliationJson
        {
            Year = year,
            Month = month,
            LockReady = readiness.IsReady,
            LockDate = lockDate == DateTime.MinValue ? null : lockDate.Date,
            IsMonthLocked = dateTo.Date <= lockDate.Date,
            IsMonthElapsed = dateTo.Date < today,
            Days = days,
            Blockers = blockers
        };
    }

    /// <summary>Lock readiness, lock context and blocker counts for the calendar month — the same evaluation as <see cref="Read"/>.</summary>
    public async Task<MonthlyLockContext> ReadLockContext(
        Guid branchId,
        int year,
        int month,
        DateTime branchLocalToday,
        CancellationToken ct = default)
    {
        var (closes, statusCounts, readiness) = await Evaluate(branchId, year, month, ct);
        var lockDate = await lockDateReader.Read(branchId, ct);
        var dateTo = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);

        return new MonthlyLockContext(
            readiness.IsReady,
            lockDate == DateTime.MinValue ? null : lockDate.Date,
            dateTo.Date <= lockDate.Date,
            dateTo.Date < branchLocalToday.Date,
            readiness.UnapprovedCloses.Count,
            statusCounts.Count(row => row is { Status: TransactionStatus.Draft, Count: > 0 }),
            readiness.MissingExpectedCloses.Count,
            closes.Count > 0 || statusCounts.Any(row => row.Count > 0));
    }

    // The month's closes, per-(Date, Status) transaction counts (Active/Draft/Cancelled) and the readiness the lock
    // command's evaluator derives from them with the direct Terminal activity pairs.
    private async Task<(IReadOnlyList<DailyClose> Closes, IReadOnlyList<MonthlyTransactionCountRow> StatusCounts, MonthLockReadinessResult Readiness)> Evaluate(
        Guid branchId, int year, int month, CancellationToken ct)
    {
        var closes = await dailyClosesRepository.ListByBranchIdAndYearMonthAsNoTracking(branchId, year, month, ct);
        var statusCounts = await transactionsRepository.CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
            branchId, year, month, ct);
        var directTerminalActivityPairs = await transactionsRepository
            .ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(branchId, year, month, ct);
        return (closes, statusCounts, readinessEvaluator.Evaluate(closes, statusCounts, directTerminalActivityPairs));
    }
}
