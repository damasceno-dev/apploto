using server.Application.Services.TimeEntries;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Reports;

/// <summary>
/// The one time-entry balance computation (§6.7), shared by <c>GET /report/timeentry-balance</c>, the monthly financial
/// report and the management overview (M11 Phases 6–7). Every entry re-runs <see cref="ITimeEntryCalculationService"/>
/// under the policy of its own date against one caller-captured branch-local now; no authorization here.
/// </summary>
public class TimeEntryBalanceProjector(
    IOperatorsRepository operatorsRepository,
    ITimeEntriesRepository timeEntriesRepository,
    ITimeEntryPoliciesRepository timeEntryPoliciesRepository,
    ITimeEntryCalculationService calculationService)
{
    /// <summary>
    /// Branch-wide roll-up: every active operator — zero-entry rows included, with or without a login link — plus any
    /// operator with entries in the window even after deactivation, ordered by name then id.
    /// </summary>
    public async Task<List<ResponseTimeEntryBalanceOperatorJson>> BuildBranchWide(
        Guid branchId, DateTime dateFrom, DateTime dateTo, DateTime branchLocalNow, CancellationToken ct = default)
    {
        var policies = await timeEntryPoliciesRepository.ListActiveByBranchIdAsNoTracking(branchId, ct);
        var entries = await timeEntriesRepository.ListByBranchIdAndDateRangeAsNoTracking(branchId, dateFrom, dateTo, ct);

        var entriesByOperator = entries
            .GroupBy(entry => entry.OperatorId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TimeEntry>)group.ToList());

        var activeOperators = await operatorsRepository.ListActiveByBranchId(branchId, ct);
        var operatorNamesById = activeOperators.ToDictionary(op => op.Id, op => op.Name);
        foreach (var group in entriesByOperator)
            operatorNamesById.TryAdd(group.Key, group.Value[0].Operator.Name);

        return
        [
            .. operatorNamesById
                .Select(pair => BuildOperatorSummary(
                    pair.Key,
                    pair.Value,
                    activeOperators.Any(op => op.Id == pair.Key),
                    entriesByOperator.GetValueOrDefault(pair.Key, []),
                    policies,
                    branchLocalNow))
                .OrderBy(summary => summary.OperatorName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(summary => summary.OperatorId)
        ];
    }

    public async Task<ResponseTimeEntryBalanceOperatorJson> BuildForOperator(
        Guid branchId, Operator targetOperator, DateTime dateFrom, DateTime dateTo, DateTime branchLocalNow, CancellationToken ct = default)
    {
        var policies = await timeEntryPoliciesRepository.ListActiveByBranchIdAsNoTracking(branchId, ct);
        var entries = await timeEntriesRepository.ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            branchId, targetOperator.Id, dateFrom, dateTo, ct);

        return BuildOperatorSummary(targetOperator.Id, targetOperator.Name, targetOperator.Active, entries, policies, branchLocalNow);
    }

    private ResponseTimeEntryBalanceOperatorJson BuildOperatorSummary(
        Guid operatorId,
        string operatorName,
        bool operatorActive,
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<TimeEntryPolicy> policies,
        DateTime branchLocalNow)
    {
        var days = new List<ResponseTimeEntryBalanceSummaryDayJson>(entries.Count);
        var totalHours = 0m;
        var totalBalanceHours = 0m;

        foreach (var entry in entries)
        {
            // Re-run Calculate per row (never read the persisted checkpoint): in-progress
            // current-day rows surface live-recomputed hours, non-Present rows resolve to
            // their abonado/owing constants, and closed rows recompute deterministically.
            // Constants come from the policy row applicable to each entry's own date, so a
            // settings change today never rewrites earlier days in the same window.
            var segments = entry.Segments
                .Where(segment => segment.Active)
                .Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut))
                .ToList();

            var policy = TimeEntryPolicyResolver.Resolve(policies, entry.Date);
            var (dayTotalHours, dayBalanceHours) = calculationService.Calculate(
                entry.Status,
                segments,
                entry.Date,
                branchLocalNow,
                policy.DailyTargetHours,
                policy.LunchDeductionOver6H,
                policy.LunchDeductionOver4H);

            totalHours += dayTotalHours;
            totalBalanceHours += dayBalanceHours;

            days.Add(new ResponseTimeEntryBalanceSummaryDayJson
            {
                Date = entry.Date,
                Status = entry.Status,
                TotalHours = dayTotalHours,
                BalanceHours = dayBalanceHours,
                IsInProgress = entry.Segments.Any(segment => segment.Active && segment.ClockOut is null)
            });
        }

        return new ResponseTimeEntryBalanceOperatorJson
        {
            OperatorId = operatorId,
            OperatorName = operatorName,
            OperatorActive = operatorActive,
            TotalHours = totalHours,
            TotalBalanceHours = totalBalanceHours,
            PresentDays = entries.Count(entry => entry.Status == TimeEntryStatus.Present),
            AbsentDays = entries.Count(entry =>
                entry.Status is TimeEntryStatus.JustifiedAbsence or TimeEntryStatus.UnjustifiedAbsence),
            OwingDays = entries.Count(entry =>
                entry.Status is TimeEntryStatus.DayOff or TimeEntryStatus.UnjustifiedAbsence),
            AbonadoDays = entries.Count(entry =>
                entry.Status is TimeEntryStatus.Sunday or TimeEntryStatus.Holiday
                    or TimeEntryStatus.Vacation or TimeEntryStatus.JustifiedAbsence),
            ContainsInProgress = entries.Any(entry =>
                entry.Segments.Any(segment => segment.Active && segment.ClockOut is null)),
            Days = days
        };
    }
}
