using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models.Projections;

namespace server.Application.Services.Settings;

public sealed record MonthLockReadinessResult(
    IReadOnlyList<DailyClose> UnapprovedCloses,
    IReadOnlyList<TerminalActivityPairRow> MissingExpectedCloses,
    bool HasDraftTransactions,
    bool HasNoCloseMonthWithExpectedActivity)
{
    public bool IsReady =>
        UnapprovedCloses.Count == 0 &&
        MissingExpectedCloses.Count == 0 &&
        HasDraftTransactions is false &&
        HasNoCloseMonthWithExpectedActivity is false;
}

public sealed class MonthLockReadinessEvaluator
{
    public MonthLockReadinessResult Evaluate(
        IReadOnlyList<DailyClose> closes,
        IReadOnlyList<MonthlyTransactionCountRow> transactionCounts,
        IReadOnlyList<TerminalActivityPairRow> directTerminalActivityPairs)
    {
        var closePairs = closes
            .Select(close => (close.AccountId, Date: close.Date.Date))
            .ToHashSet();

        var missingExpectedCloses = directTerminalActivityPairs
            .Where(activity => closePairs.Contains((activity.AccountId, activity.Date.Date)) is false)
            .DistinctBy(activity => (activity.AccountId, activity.Date.Date))
            .OrderBy(activity => activity.Date)
            .ThenBy(activity => activity.AccountName)
            .ThenBy(activity => activity.AccountId)
            .ToList();

        var unapprovedCloses = closes
            .Where(close => close.Status != DailyCloseStatus.Approved)
            .OrderBy(close => close.Date)
            .ThenBy(close => close.Account.Name)
            .ThenBy(close => close.Id)
            .ToList();

        return new MonthLockReadinessResult(
            unapprovedCloses,
            missingExpectedCloses,
            transactionCounts.Any(row => row is { Status: TransactionStatus.Draft, Count: > 0 }),
            closes.Count == 0 && directTerminalActivityPairs.Count > 0);
    }
}
