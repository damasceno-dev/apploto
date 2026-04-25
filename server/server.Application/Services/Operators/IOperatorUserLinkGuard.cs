namespace server.Application.Services.Operators;

public interface IOperatorUserLinkGuard
{
    Task EnsureLinkable(Guid userId, Guid branchId, Guid? exceptOperatorId = null);
}
