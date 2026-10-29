using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Handover;

/// <summary>
/// DisputeSessionHandover / WithdrawSessionHandover (§6.20). A dispute (intended incoming) records disagreement and
/// leaves custody unchanged; a withdrawal (outgoing custodian or manager) cancels an unaccepted offer. Neither writes
/// financial rows nor rewrites a count; an accepted handover cannot be withdrawn.
/// </summary>
public class ResolveSessionHandoverOfferUseCase(
    IAuthenticationService authenticationService,
    ISessionHandoversRepository sessionHandoversRepository,
    IDailyClosesRepository dailyClosesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    SessionEventRecorder eventRecorder,
    SessionCommandRunner commandRunner,
    IBranchClock branchClock)
{
    public Task<ResponseSessionHandoverJson> Dispute(
        Guid handoverId,
        RequestSessionReasonJson request,
        uint expectedVersion,
        CancellationToken ct = default) => Resolve(handoverId, request, expectedVersion, dispute: true, ct);

    public Task<ResponseSessionHandoverJson> Withdraw(
        Guid handoverId,
        RequestSessionReasonJson request,
        uint expectedVersion,
        CancellationToken ct = default) => Resolve(handoverId, request, expectedVersion, dispute: false, ct);

    private async Task<ResponseSessionHandoverJson> Resolve(
        Guid handoverId,
        RequestSessionReasonJson request,
        uint expectedVersion,
        bool dispute,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new OnValidationException([ResourcesErrorMessages.SESSION_REASON_REQUIRED]);
        SessionStarter.EnsureReasonLength(request.Reason);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var handoverKey = await sessionHandoversRepository.GetByIdAndBranchIdAsNoTracking(handoverId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_FOUND);

        return await commandRunner.RunGuarded<ResponseSessionHandoverJson>(
            branchUser,
            [handoverKey.AccountId],
            async () =>
            {
                var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
                var handover = await sessionHandoversRepository.GetByIdAndBranchId(handoverId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_FOUND);
                if (handover.Version != expectedVersion)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_STALE);

                var outgoing = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(
                    handover.OutgoingSessionId,
                    branchUser.BranchId,
                    ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);

                if (dispute)
                {
                    if (handover.State != SessionHandoverState.Offered)
                        throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_PENDING);
                    if (memberScope.LinkedOperator?.Id != handover.IntendedIncomingOperatorId)
                        throw new TokenWithoutPermissionException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_INTENDED_INCOMING);
                    handover.State = SessionHandoverState.Disputed;
                }
                else
                {
                    if (handover.State is not (SessionHandoverState.Offered or SessionHandoverState.Disputed))
                        throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_PENDING);
                    SessionCustodyRules.EnsureCustodianOrManagerException(outgoing, branchUser, memberScope, request.Reason);
                    handover.State = SessionHandoverState.Withdrawn;
                    handover.ResolvedByUserId = branchUser.UserId;
                    handover.ResolvedAt = branchClock.UtcNow();
                }

                var reason = request.Reason!.Trim();
                handover.Reason = reason;
                await eventRecorder.Record(
                    outgoing,
                    dispute ? SessionEventKind.HandoverDisputed : SessionEventKind.HandoverWithdrawn,
                    branchUser.User,
                    branchClock.UtcNow(),
                    new { HandoverId = handover.Id, handover.State },
                    reason,
                    handover.Id,
                    ct);

                return async () =>
                {
                    var saved = await sessionHandoversRepository.GetByIdAndBranchIdAsNoTracking(handoverId, branchUser.BranchId, ct);
                    return saved!.ToResponse();
                };
            },
            ct);
    }
}
