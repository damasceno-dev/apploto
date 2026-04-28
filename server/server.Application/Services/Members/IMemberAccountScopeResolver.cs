namespace server.Application.Services.Members;

public interface IMemberAccountScopeResolver
{
    Task<MemberAccountScope> Resolve(Guid userId, Guid branchId);
}
