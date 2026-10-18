using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Domain.Interfaces;

public interface IOnboardingRepository
{
    Task<OperatorLinkRequest?> GetRequest(Guid id, Guid branchId);
    Task<OperatorLinkRequest?> GetOwnRequest(Guid userId, Guid branchId);
    Task AddRequest(OperatorLinkRequest request);
    Task<(IReadOnlyList<OperatorLinkRequest> Items, int Total)> ListRequests(Guid branchId, OperatorLinkRequestStatus? status, int page, int pageSize);
    Task<int> PendingRequestCount(Guid branchId);
    Task<Invitation?> GetInvitation(Guid id, Guid branchId);
    Task<Invitation?> GetInvitationByHash(string hash);
    Task<Invitation?> GetPendingInvitation(Guid branchId, string email);
    Task AddInvitation(Invitation invitation);
    Task<(IReadOnlyList<Invitation> Items, int Total)> ListInvitations(Guid branchId, bool includeAdmin, InvitationStatus? status, int page, int pageSize, DateTime now);
}
