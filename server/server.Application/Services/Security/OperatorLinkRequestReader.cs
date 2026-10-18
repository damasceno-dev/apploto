using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Security;

public sealed class OperatorLinkRequestReader(IOperatorsRepository operators, IOperatorAccountsRepository accounts)
{
    public async Task<(Guid? OperatorId, bool HasUsableAccount)> State(Guid userId, Guid branchId)
    {
        var op = await operators.GetActiveLinkedByUserIdAndBranchIdAsNoTracking(userId, branchId);
        if (op is null) return (null, false);
        var links = await accounts.ListActiveByOperatorIdWithAccount(op.Id);
        return (op.Id, links.Any(l => l.Active && l.Account.Active && l.Account.BranchId == branchId && l.Account.Type == AccountType.Terminal));
    }
    public static ResponseOperatorLinkRequestJson Map(OperatorLinkRequest request, string name, (Guid? OperatorId, bool HasUsableAccount) state) => new()
    {
        Id = request.Id, UserId = request.UserId, UserName = name, Status = request.Status,
        CreatedAt = request.CreatedAt, ResolvedAt = request.ResolvedAt,
        OperatorId = state.OperatorId, HasUsableAccount = state.HasUsableAccount
    };

}
