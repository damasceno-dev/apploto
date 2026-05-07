using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

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
                ClockIn = timeEntry.ClockIn,
                ClockOut = timeEntry.ClockOut,
                Status = timeEntry.Status,
                TotalHours = timeEntry.TotalHours,
                BalanceHours = timeEntry.BalanceHours,
                IsInProgress = timeEntry.Status == TimeEntryStatus.Present
                    && timeEntry.ClockIn is not null
                    && timeEntry.ClockOut is null,
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
