using server.Domain.Entities;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.DailyCloses;

public interface IDailyCloseWorkflowGuard
{
    void EnsureCanOpen(BranchUser caller, Operator? callerOperator, Guid accountId, DateTime branchLocalDate);
    DailyCloseEditItemsOutcome EnsureCanEditItems(DailyClose close, BranchUser caller, Operator? callerOperator);
    void EnsureCanSubmit(DailyClose close, BranchUser caller, Operator? callerOperator);
    void EnsureCanApprove(DailyClose close, BranchUser caller);
    void EnsureCanReject(DailyClose close, BranchUser caller);
}
