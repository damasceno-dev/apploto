using server.Application.Services.TimeEntries;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.TimeEntries;

/// <summary>
/// Shared projection helper for the Get and List read paths. Applies the read-time
/// live-running recompute when any active segment has <c>ClockOut == null</c> (the
/// persisted <c>TotalHours</c>/<c>BalanceHours</c> are a last-write checkpoint that
/// may be stale by the time the row is read); rows where every active segment is
/// already closed pass the persisted values through unchanged. <c>IsInProgress</c>
/// is always computed from the segment set, never read from storage.
/// </summary>
public static class TimeEntryReadSharedMapper
{
    extension(TimeEntry timeEntry)
    {
        public ResponseTimeEntryJson ToReadResponse(
            string operatorName,
            Setting setting,
            DateTime branchLocalNow,
            ITimeEntryCalculationService calculationService)
        {
            var activeSegments = OrderedActiveSegments(timeEntry).ToList();
            var (totalHours, balanceHours, isInProgress) = ResolveTotals(
                timeEntry,
                activeSegments,
                setting,
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
            Setting setting,
            DateTime branchLocalNow,
            ITimeEntryCalculationService calculationService)
        {
            var activeSegments = OrderedActiveSegments(timeEntry).ToList();
            var (totalHours, balanceHours, isInProgress) = ResolveTotals(
                timeEntry,
                activeSegments,
                setting,
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
        Setting setting,
        DateTime branchLocalNow,
        ITimeEntryCalculationService calculationService)
    {
        var isInProgress = activeSegments.Any(segment => segment.ClockOut is null);

        if (isInProgress is false)
            return (timeEntry.TotalHours, timeEntry.BalanceHours, false);

        var (totalHours, balanceHours) = calculationService.Calculate(
            timeEntry.Status,
            activeSegments.Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut)).ToList(),
            timeEntry.Date,
            branchLocalNow,
            setting.DailyTargetHours,
            setting.LunchDeductionOver6H,
            setting.LunchDeductionOver4H);

        return (totalHours, balanceHours, true);
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
