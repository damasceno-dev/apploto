using server.Application.Services.TimeEntries;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.TimeEntries;

/// <summary>
/// Shared projection helper for the Get and List read paths. Recomputes
/// <c>TotalHours</c>/<c>BalanceHours</c> for every row under the effective-dated policy
/// resolved for the entry's own date — the persisted values are only a last-write
/// checkpoint, stale for a live-running row by the time it is read and stale for a
/// closed row after a same-day policy change. Recomputing unconditionally keeps Get,
/// List, and the §6.14 balance report answering with one number (§6.7: read endpoints
/// recompute on every call). <c>IsInProgress</c> is always computed from the segment
/// set, never read from storage.
/// </summary>
public static class TimeEntryReadSharedMapper
{
    extension(TimeEntry timeEntry)
    {
        public ResponseTimeEntryJson ToReadResponse(
            string operatorName,
            TimeEntryPolicy policy,
            DateTime branchLocalNow,
            ITimeEntryCalculationService calculationService)
        {
            var activeSegments = OrderedActiveSegments(timeEntry).ToList();
            var (totalHours, balanceHours, isInProgress) = ResolveTotals(
                timeEntry,
                activeSegments,
                policy,
                branchLocalNow,
                calculationService);

            return new ResponseTimeEntryJson
            {
                Id = timeEntry.Id,
                Date = timeEntry.Date,
                Status = timeEntry.Status,
                TotalHours = totalHours,
                BalanceHours = balanceHours,
                IsInProgress = isInProgress,
                Segments = activeSegments
                    .Select(segment => new ResponseTimeEntrySegmentJson
                    {
                        Id = segment.Id,
                        ClockIn = segment.ClockIn,
                        ClockOut = segment.ClockOut,
                        CreatedAt = segment.CreatedAt,
                        UpdatedAt = segment.UpdatedAt,
                        UpdatedByUserId = segment.UpdatedByUserId,
                        Active = segment.Active
                    })
                    .ToList(),
                OperatorId = timeEntry.OperatorId,
                OperatorName = operatorName,
                BranchId = timeEntry.BranchId,
                CreatedAt = timeEntry.CreatedAt,
                UpdatedAt = timeEntry.UpdatedAt,
                UpdatedByUserId = timeEntry.UpdatedByUserId,
                Active = timeEntry.Active
            };
        }

        public ResponseListTimeEntryItemJson ToListItemResponse(
            string operatorName,
            TimeEntryPolicy policy,
            DateTime branchLocalNow,
            ITimeEntryCalculationService calculationService)
        {
            var activeSegments = OrderedActiveSegments(timeEntry).ToList();
            var (totalHours, balanceHours, isInProgress) = ResolveTotals(
                timeEntry,
                activeSegments,
                policy,
                branchLocalNow,
                calculationService);

            return new ResponseListTimeEntryItemJson
            {
                Id = timeEntry.Id,
                Date = timeEntry.Date,
                Status = timeEntry.Status,
                TotalHours = totalHours,
                BalanceHours = balanceHours,
                IsInProgress = isInProgress,
                OperatorId = timeEntry.OperatorId,
                OperatorName = operatorName,
                BranchId = timeEntry.BranchId,
                CreatedAt = timeEntry.CreatedAt,
                UpdatedAt = timeEntry.UpdatedAt,
                UpdatedByUserId = timeEntry.UpdatedByUserId,
                Active = timeEntry.Active
            };
        }
    }

    private static (decimal TotalHours, decimal BalanceHours, bool IsInProgress) ResolveTotals(
        TimeEntry timeEntry,
        IReadOnlyList<TimeEntrySegment> activeSegments,
        TimeEntryPolicy policy,
        DateTime branchLocalNow,
        ITimeEntryCalculationService calculationService)
    {
        var isInProgress = activeSegments.Any(segment => segment.ClockOut is null);

        var (totalHours, balanceHours) = calculationService.Calculate(
            timeEntry.Status,
            [.. activeSegments.Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut))],
            timeEntry.Date,
            branchLocalNow,
            policy.DailyTargetHours,
            policy.LunchDeductionOver6H,
            policy.LunchDeductionOver4H);

        return (totalHours, balanceHours, isInProgress);
    }

    private static IEnumerable<TimeEntrySegment> OrderedActiveSegments(TimeEntry timeEntry)
    {
        return timeEntry.Segments
            .Where(segment => segment.Active)
            .OrderBy(segment => segment.ClockIn)
            .ThenBy(segment => segment.CreatedAt)
            .ThenBy(segment => segment.Id);
    }
}
