using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Handover;

/// <summary>
/// OfferSessionHandover (§6.20): the outgoing custodian offers the submitted, counted cashbox to a named incoming
/// operator. Custody stays with the outgoing operator until acceptance; no financial rows are written.
/// </summary>
public class OfferSessionHandoverUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    ISessionHandoversRepository sessionHandoversRepository,
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    ICashVarianceProductResolver cashVarianceProductResolver,
    SessionEventRecorder eventRecorder,
    SessionCommandRunner commandRunner,
    IBranchClock branchClock)
{
    private const string IdempotencyEndpoint = "POST /session/{id}/handover";

    public async Task<ResponseSessionHandoverJson> Execute(
        Guid sessionId,
        RequestOfferSessionHandoverJson request,
        uint expectedVersion,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        if (request.IncomingOperatorId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.SESSION_HANDOVER_INCOMING_NOT_FOUND]);
        SessionStarter.EnsureReasonLength(request.Reason);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var sessionKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);

        return await commandRunner.Run<object, ResponseSessionHandoverJson>(
            IdempotencyEndpoint,
            idempotencyKey,
            branchUser,
            new { SessionId = sessionId, ExpectedVersion = expectedVersion, Request = request },
            [sessionKey.AccountId],
            async () =>
            {
                var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
                var session = await dailyClosesRepository.GetByIdAndBranchId(sessionId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
                memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, session.AccountId);

                SessionCustodyRules.EnsureActiveCustody(session);
                SessionCustodyRules.EnsureCustodianOrManagerException(session, branchUser, memberScope, request.Reason);
                if (session.Version != expectedVersion)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);
                if (session.Status != DailyCloseStatus.Submitted || session.ItemsFirstRecordedAt is null)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_REQUIRES_COUNT);
                if (session.CountRevision != request.CountRevision)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_COUNT_CHANGED);
                if (await sessionHandoversRepository.GetUnresolvedByOutgoingSessionId(branchUser.BranchId, session.Id, ct) is not null)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_PENDING);

                var incoming = await operatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(
                    request.IncomingOperatorId,
                    branchUser.BranchId,
                    ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_HANDOVER_INCOMING_NOT_FOUND);
                if (incoming.Id == session.CustodianOperatorId)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_INCOMING_SAME_AS_OUTGOING);
                var grant = await operatorAccountsRepository.GetByOperatorIdAndAccountId(incoming.Id, session.AccountId);
                if (grant is not { Active: true })
                    throw new ConflictException(ResourcesErrorMessages.SESSION_CUSTODIAN_NOT_GRANTED);

                var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
                var now = branchClock.UtcNow();
                var handover = new SessionHandover
                {
                    BranchId = branchUser.BranchId,
                    AccountId = session.AccountId,
                    OutgoingSessionId = session.Id,
                    IntendedIncomingOperatorId = incoming.Id,
                    OfferedCountRevision = session.CountRevision,
                    OfferedByUserId = branchUser.UserId,
                    OfferedAt = now,
                    Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    CreatedAt = now
                };
                foreach (var item in session.Items.Where(item => item.Active && item.ProductId != cashVarianceProductId))
                {
                    handover.Items.Add(new SessionHandoverItem
                    {
                        SessionHandoverId = handover.Id,
                        ProductId = item.ProductId,
                        Value = item.Value,
                        CreatedAt = now
                    });
                }
                await sessionHandoversRepository.Add(handover, ct);
                await eventRecorder.Record(
                    session,
                    SessionEventKind.HandoverOffered,
                    branchUser.User,
                    now,
                    new { HandoverId = handover.Id, IncomingOperatorId = incoming.Id, session.CountRevision },
                    handover.Reason,
                    handover.Id,
                    ct);

                return async () =>
                {
                    var saved = await sessionHandoversRepository.GetByIdAndBranchIdAsNoTracking(handover.Id, branchUser.BranchId, ct);
                    return (handover.Id, saved!.ToResponse());
                };
            },
            ct);
    }
}
