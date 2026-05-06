using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.TimeEntries;

public interface ITimeEntryWritePermissionGuard
{
    TimeEntryWritePermissionOutcome Evaluate(
        BranchUser caller,
        Operator? callerOperator,
        Guid targetOperatorId,
        DateTime targetDate,
        TimeEntryStatus status,
        DateTime utcNow);
}
