using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Sessions;

/// <summary>
/// A changed outgoing closing invalidates any unaccepted offer (§6.20): recall, reopen or rejection withdraws it, so
/// what the incoming person would accept is never silently refreshed.
/// </summary>
public sealed class SessionHandoverInvalidator(
    ISessionHandoversRepository sessionHandoversRepository,
    SessionEventRecorder eventRecorder)
{
    public async Task WithdrawPending(DailyClose session, User actor, DateTime now, string reason, CancellationToken ct = default)
    {
        var pending = await sessionHandoversRepository.GetUnresolvedByOutgoingSessionId(session.BranchId, session.Id, ct);
        if (pending is null)
            return;

        pending.State = SessionHandoverState.Withdrawn;
        pending.ResolvedByUserId = actor.Id;
        pending.ResolvedAt = now;
        pending.Reason = reason;
        await eventRecorder.Record(
            session,
            SessionEventKind.HandoverWithdrawn,
            actor,
            now,
            new { HandoverId = pending.Id, Automatic = true },
            reason,
            pending.Id,
            ct);
    }
}
