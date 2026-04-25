namespace server.Application.Services.Transactions;

public interface IMemberTransactionScopeResolver
{
    Task<MemberTransactionScope> Resolve(Guid userId, Guid branchId);
}
