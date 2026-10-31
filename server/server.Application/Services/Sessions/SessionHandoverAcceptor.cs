using server.Application.Services.Members;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Sessions;

public sealed record SessionHandoverAcceptance(SessionHandover Handover, DailyClose Outgoing, SessionStartResult Incoming);

/// <summary>
/// Accepts a pending handover in one commit (§6.20): the outgoing session ends HandedOver at the instant the incoming
/// session starts from the incoming operator's own acknowledged vector. The offered and acknowledged vectors are both
/// kept; a difference becomes an Unattributed opening discrepancy linked to the handover. The caller holds the
/// cashbox coordination and commits.
/// </summary>
public sealed class SessionHandoverAcceptor(
    IDailyClosesRepository dailyClosesRepository,
    IAccountsRepository accountsRepository,
    SessionStarter sessionStarter,
    SessionEventRecorder eventRecorder,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<SessionHandoverAcceptance> Accept(
        SessionHandover handover,
        uint? expectedVersion,
        BranchUser actor,
        MemberAccountScope scope,
        IReadOnlyList<RequestSessionOpeningItemJson>? items,
        bool explicitZero,
        string? reason,
        CancellationToken ct = default)
    {
        if (handover.State is not (SessionHandoverState.Offered or SessionHandoverState.Disputed))
            throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_PENDING);
        if (expectedVersion is not { } version || version != handover.Version)
            throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_STALE);
        if (scope.LinkedOperator?.Id != handover.IntendedIncomingOperatorId)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_INTENDED_INCOMING);

        return await Complete(handover, actor, scope, items, explicitZero, reason, SessionOrigin.HandoverAccepted, false, ct);
    }

    /// <summary>The manager AcceptAndStart exception: same transition, evidence ManagerException, reason mandatory.</summary>
    public Task<SessionHandoverAcceptance> AcceptAsManagerException(
        SessionHandover handover,
        BranchUser actor,
        MemberAccountScope scope,
        Guid incomingOperatorId,
        IReadOnlyList<RequestSessionOpeningItemJson>? items,
        bool explicitZero,
        string reason,
        CancellationToken ct = default)
    {
        if (handover.State is not (SessionHandoverState.Offered or SessionHandoverState.Disputed))
            throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_PENDING);
        if (incomingOperatorId != handover.IntendedIncomingOperatorId)
            throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_INTENDED_INCOMING);

        return Complete(handover, actor, scope, items, explicitZero, reason, SessionOrigin.ManagerException, true, ct,
            incomingOperatorId);
    }

    private async Task<SessionHandoverAcceptance> Complete(
        SessionHandover handover,
        BranchUser actor,
        MemberAccountScope scope,
        IReadOnlyList<RequestSessionOpeningItemJson>? items,
        bool explicitZero,
        string? reason,
        SessionOrigin origin,
        bool managerException,
        CancellationToken ct,
        Guid? incomingOperatorId = null)
    {
        var outgoing = await dailyClosesRepository.GetByIdAndBranchId(handover.OutgoingSessionId, actor.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        if (outgoing.CustodyState != SessionCustodyState.Active)
            throw new ConflictException(ResourcesErrorMessages.SESSION_NOT_ACTIVE_CUSTODY);
        // The offer stays valid only while the outgoing submission it carried is the current one (count and attempt).
        if (outgoing.Status != DailyCloseStatus.Submitted
            || outgoing.CountRevision != handover.OfferedCountRevision
            || (handover.OfferedAttemptId is not null && outgoing.SubmittedAttemptId != handover.OfferedAttemptId))
            throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_COUNT_CHANGED);

        var cashbox = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(handover.AccountId, actor.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        // End the outgoing custody first and flush inside the coordinated transaction, so the single-active-custody
        // index and the interval trigger see the exact handover boundary before the incoming session is inserted.
        var now = branchClock.UtcNow();
        outgoing.CustodyState = SessionCustodyState.HandedOver;
        outgoing.EndedAt = now;
        outgoing.UpdatedAt = now;
        outgoing.UpdatedByUserId = actor.UserId;
        await unitOfWork.Commit(ct);

        var incoming = await sessionStarter.Start(new SessionStartCommand(
            cashbox,
            actor,
            scope,
            incomingOperatorId,
            origin,
            explicitZero,
            items,
            reason,
            Handover: handover,
            ForceManagerException: managerException,
            StartedAt: now), ct);

        handover.State = SessionHandoverState.Accepted;
        handover.IncomingSessionId = incoming.Session.Id;
        handover.ResolvedByUserId = actor.UserId;
        handover.ResolvedAt = now;

        await eventRecorder.Record(
            outgoing,
            managerException ? SessionEventKind.CustodyExceptionResolved : SessionEventKind.HandoverAccepted,
            actor.User,
            now,
            new { HandoverId = handover.Id, IncomingSessionId = incoming.Session.Id, EndedAt = now },
            managerException ? reason : null,
            handover.Id,
            ct);
        await eventRecorder.Record(
            incoming.Session,
            SessionEventKind.HandoverAccepted,
            actor.User,
            now,
            new { HandoverId = handover.Id, OutgoingSessionId = outgoing.Id, Discrepancy = incoming.Discrepancy?.Id },
            relatedId: handover.Id,
            ct: ct);

        return new SessionHandoverAcceptance(handover, outgoing, incoming);
    }
}
