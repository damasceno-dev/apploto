using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.TimeEntries.Upsert;

public static class UpsertTimeEntryMapper
{
    extension(TimeEntry timeEntry)
    {
        public ResponseTimeEntryJson ToResponse(string operatorName)
        {
            return new ResponseTimeEntryJson
            {
                Id = timeEntry.Id,
                Date = timeEntry.Date,
                Status = timeEntry.Status,
                TotalHours = timeEntry.TotalHours,
                BalanceHours = timeEntry.BalanceHours,
                IsInProgress = timeEntry.Segments.Any(segment => segment.Active && segment.ClockOut is null),
                Segments = timeEntry.Segments
                    .Where(segment => segment.Active)
                    .OrderBy(segment => segment.ClockIn)
                    .ThenBy(segment => segment.CreatedAt)
                    .ThenBy(segment => segment.Id)
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
    }
}
