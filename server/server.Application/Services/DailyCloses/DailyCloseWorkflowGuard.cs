using server.Domain.Entities;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.DailyCloses;

public class DailyCloseWorkflowGuard : IDailyCloseWorkflowGuard
{
    public void EnsureCanOpen(BranchUser caller, Operator? callerOperator, Guid accountId, DateTime branchLocalDate)
    {
    }

    public DailyCloseEditItemsOutcome EnsureCanEditItems(
        DailyClose close,
        BranchUser caller,
        Operator? callerOperator)
    {
        throw new NotImplementedException();
    }

    public void EnsureCanSubmit(DailyClose close, BranchUser caller, Operator? callerOperator)
    {
        throw new NotImplementedException();
    }

    public void EnsureCanApprove(DailyClose close, BranchUser caller)
    {
        throw new NotImplementedException();
    }

    public void EnsureCanReject(DailyClose close, BranchUser caller)
    {
        throw new NotImplementedException();
    }
}
