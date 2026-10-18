using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal sealed class OnboardingRepository(ServerDbContext db) : IOnboardingRepository
{
    private IQueryable<OperatorLinkRequest> EligibleRequests(Guid branchId) => db.OperatorLinkRequests
        .Where(r => r.BranchId == branchId && r.Active && r.User.Active && r.Branch.Active &&
            db.BranchUsers.Any(m => m.BranchId == branchId && m.UserId == r.UserId && m.Active && m.Role == Role.Member));
    public Task<OperatorLinkRequest?> GetRequest(Guid id, Guid branchId) => EligibleRequests(branchId).Include(r => r.User)
        .SingleOrDefaultAsync(r => r.Id == id);
    public Task<OperatorLinkRequest?> GetOwnRequest(Guid userId, Guid branchId) => EligibleRequests(branchId).Include(r => r.User)
        .Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync();
    public async Task AddRequest(OperatorLinkRequest request) => await db.OperatorLinkRequests.AddAsync(request);
    public async Task<(IReadOnlyList<OperatorLinkRequest> Items, int Total)> ListRequests(Guid branchId, OperatorLinkRequestStatus? status, int page, int pageSize)
    {
        var query = EligibleRequests(branchId).AsNoTracking();
        if (status.HasValue) query = query.Where(r => r.Status == status);
        var total = await query.CountAsync();
        var items = await query.Include(r => r.User).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }
    public Task<int> PendingRequestCount(Guid branchId) => EligibleRequests(branchId).CountAsync(r => r.Status == OperatorLinkRequestStatus.Pending);
    public async Task<Invitation?> GetInvitation(Guid id, Guid branchId)
    {
        var invitation = await db.Invitations.Include(i => i.Branch)
            .SingleOrDefaultAsync(i => i.Id == id && i.BranchId == branchId && i.Active && i.Branch.Active);
        if (invitation is not null) await db.Entry(invitation).ReloadAsync();
        return invitation;
    }
    public async Task<Invitation?> GetInvitationByHash(string hash)
    {
        var invitation = await db.Invitations.Include(i => i.Branch)
            .SingleOrDefaultAsync(i => i.TokenHash == hash && i.Active && i.Branch.Active);
        if (invitation is not null) await db.Entry(invitation).ReloadAsync();
        return invitation;
    }
    public Task<Invitation?> GetPendingInvitation(Guid branchId, string email) => db.Invitations
        .SingleOrDefaultAsync(i => i.BranchId == branchId && i.Email == email && i.Active && i.Status == InvitationStatus.Pending);
    public async Task AddInvitation(Invitation invitation) => await db.Invitations.AddAsync(invitation);
    public async Task<(IReadOnlyList<Invitation> Items, int Total)> ListInvitations(Guid branchId, bool includeAdmin, InvitationStatus? status, int page, int pageSize, DateTime now)
    {
        var query = db.Invitations.AsNoTracking().Where(i => i.BranchId == branchId && i.Active && i.Branch.Active);
        if (!includeAdmin) query = query.Where(i => i.Role != Role.Admin);
        if (status == InvitationStatus.Expired)
            query = query.Where(i => i.Status == InvitationStatus.Expired || (i.Status == InvitationStatus.Pending && i.ExpiresAt <= now));
        else if (status == InvitationStatus.Pending)
            query = query.Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt > now);
        else if (status.HasValue) query = query.Where(i => i.Status == status);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }
}
