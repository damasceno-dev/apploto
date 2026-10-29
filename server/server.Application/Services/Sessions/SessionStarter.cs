using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Sessions;

/// <summary>What a start acknowledges and on whose authority. The caller already holds the cashbox coordination.</summary>
public sealed record SessionStartCommand(
    Account Cashbox,
    BranchUser Actor,
    MemberAccountScope ActorScope,
    Guid? RequestedCustodianOperatorId,
    SessionOrigin Origin,
    bool ExplicitZero,
    IReadOnlyList<RequestSessionOpeningItemJson>? Items,
    string? Reason,
    SessionOpeningSourceKind? EchoedSourceKind = null,
    Guid? EchoedSourceId = null,
    long? EchoedSourceRevision = null,
    SessionHandover? Handover = null,
    bool ForceManagerException = false,
    DateTime? StartedAt = null);

public sealed record SessionStartResult(
    DailyClose Session,
    SessionOpening Opening,
    SessionOpeningDiscrepancy? Discrepancy);

/// <summary>
/// The single path that creates Active custody (StartSession, inline opening on a financial command, handover
/// acceptance and the manager AcceptAndStart exception). Start, opening, optional discrepancy and events are staged in
/// the caller's unit of work, so they commit with — or roll back with — the caller's own effects.
/// </summary>
public sealed class SessionStarter(
    IDailyClosesRepository dailyClosesRepository,
    ISessionOpeningsRepository sessionOpeningsRepository,
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository,
    IProductsRepository productsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    SessionOpeningResolver openingResolver,
    OpeningDiscrepancyRecorder discrepancyRecorder,
    SessionEventRecorder eventRecorder,
    MemberAccountScopeGuard memberAccountScopeGuard,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock)
{
    public const int ReasonMaxLength = 500;

    public async Task<SessionStartResult> Start(SessionStartCommand command, CancellationToken ct = default)
    {
        var cashbox = command.Cashbox;
        var actor = command.Actor;
        var branchId = actor.BranchId;

        if (cashbox.Type != AccountType.Terminal || cashbox.Active is false)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_NOT_TERMINAL);

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(actor.Role, command.ActorScope, cashbox.Id);
        EnsureReasonLength(command.Reason);

        var (custodian, evidence) = await ResolveCustodian(command, ct);

        if (await dailyClosesRepository.GetActiveCustodyByAccountIdAsNoTracking(branchId, cashbox.Id, ct) is not null)
            throw new ConflictException(ResourcesErrorMessages.SESSION_ACTIVE_CUSTODY_CONFLICT);

        if (await dailyClosesRepository.HasUnresolvedHistoryByAccountIdAsNoTracking(branchId, cashbox.Id, ct))
            throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_HISTORY);

        if (cashbox.TerminalId is null)
            throw new ConflictException(ResourcesErrorMessages.SESSION_CASHBOX_TERMINAL_REQUIRED);

        // A handover passes the outgoing session's end instant, so the half-open intervals meet exactly.
        var now = command.StartedAt ?? branchClock.UtcNow();
        var businessDate = branchClock.LocalBusinessDate(now);
        await lockDateGuard.EnsureNotLocked(branchId, businessDate, ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION, ct);

        SessionOpeningProposal proposal;
        Guid? predecessorSessionId;
        if (command.Handover is { } handover)
        {
            proposal = new SessionOpeningProposal(
                SessionOpeningSourceKind.PreviousSession,
                handover.OutgoingSessionId,
                handover.OfferedCountRevision,
                businessDate,
                handover.Items.ToDictionary(item => item.ProductId, item => item.Value));
            predecessorSessionId = handover.OutgoingSessionId;
        }
        else
        {
            proposal = await openingResolver.ProposeForNextSession(branchId, cashbox.Id, ct);
            if (proposal.SourceKind != command.EchoedSourceKind
                || proposal.SourceId != command.EchoedSourceId
                || proposal.SourceRevision != command.EchoedSourceRevision)
            {
                throw new ConflictException(proposal.IsMissing && command.EchoedSourceKind is not null
                    ? ResourcesErrorMessages.SESSION_OPENING_SOURCE_MISSING
                    : ResourcesErrorMessages.SESSION_OPENING_SOURCE_CHANGED);
            }

            predecessorSessionId = proposal.SourceKind == SessionOpeningSourceKind.PreviousSession
                ? proposal.SourceId
                : null;
        }

        var acknowledged = await ResolveAcknowledgedVector(branchId, command, proposal, ct);

        var session = new DailyClose
        {
            BranchId = branchId,
            AccountId = cashbox.Id,
            Date = businessDate,
            Status = DailyCloseStatus.Draft,
            OpenedByUserId = actor.UserId,
            MappingState = SessionMappingState.Ready,
            Origin = command.Origin,
            CustodyState = SessionCustodyState.Active,
            CustodianOperatorId = custodian.Id,
            CustodianUserIdAtStart = custodian.UserId,
            TerminalId = cashbox.TerminalId,
            Sequence = await dailyClosesRepository.GetMaxSequenceByAccountId(branchId, cashbox.Id, ct) + 1,
            StartedAt = now,
            CreatedAt = now
        };
        await dailyClosesRepository.Add(session, ct);

        var opening = new SessionOpening
        {
            BranchId = branchId,
            SessionId = session.Id,
            EvidenceKind = evidence,
            SourceKind = proposal.SourceKind ?? SessionOpeningSourceKind.ExplicitZero,
            SourceId = proposal.SourceId,
            SourceRevision = proposal.SourceRevision,
            HandoverId = command.Handover?.Id,
            AcknowledgedByUserId = actor.UserId,
            AcknowledgedByOperatorId = command.ActorScope.LinkedOperator?.Id,
            AcknowledgedAt = now,
            Reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim(),
            CreatedAt = now
        };
        foreach (var productId in proposal.Values.Keys.Union(acknowledged.Keys).Order())
        {
            opening.Items.Add(new SessionOpeningItem
            {
                SessionOpeningId = opening.Id,
                ProductId = productId,
                ProposedValue = proposal.IsMissing ? null : proposal.Values.GetValueOrDefault(productId),
                AcknowledgedValue = acknowledged[productId],
                CreatedAt = now
            });
        }
        await sessionOpeningsRepository.Add(opening, ct);

        await eventRecorder.Record(
            session,
            SessionEventKind.SessionStarted,
            actor.User,
            now,
            new { session.Sequence, CustodianOperatorId = custodian.Id, session.TerminalId, session.Origin, session.StartedAt },
            opening.Reason,
            command.Handover?.Id,
            ct);
        await eventRecorder.Record(
            session,
            SessionEventKind.OpeningAcknowledged,
            actor.User,
            now,
            new
            {
                SessionOpeningId = opening.Id,
                opening.SourceKind,
                opening.SourceId,
                opening.SourceRevision,
                Evidence = evidence,
                Total = acknowledged.Values.Sum()
            },
            relatedId: opening.Id,
            ct: ct);

        var discrepancy = proposal.IsMissing
            ? null
            : await discrepancyRecorder.OpenIfDifferent(session, opening, predecessorSessionId, actor.User, now, ct);

        return new SessionStartResult(session, opening, discrepancy);
    }

    public static void EnsureReasonLength(string? reason)
    {
        if (reason is { Length: > ReasonMaxLength })
            throw new OnValidationException([ResourcesErrorMessages.SESSION_REASON_TOO_LONG]);
    }

    private async Task<(Operator Custodian, SessionOpeningEvidenceKind Evidence)> ResolveCustodian(
        SessionStartCommand command,
        CancellationToken ct)
    {
        var actor = command.Actor;
        var ownOperator = command.ActorScope.LinkedOperator;

        if (actor.Role is not (Role.Manager or Role.Admin))
        {
            if (ownOperator is null)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
            if (command.RequestedCustodianOperatorId is { } requested && requested != ownOperator.Id)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.SESSION_RESTRICTED_START);
            if (command.ForceManagerException)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

            return (ownOperator, SessionOpeningEvidenceKind.IncomingAcknowledgment);
        }

        var custodianId = command.RequestedCustodianOperatorId ?? ownOperator?.Id
            ?? throw new OnValidationException([ResourcesErrorMessages.SESSION_CUSTODIAN_NOT_FOUND]);

        if (ownOperator is not null && custodianId == ownOperator.Id && command.ForceManagerException is false)
            return (ownOperator, SessionOpeningEvidenceKind.IncomingAcknowledgment);

        // A manager recording for another custodian is an explicit, reasoned exception: no acknowledgment is
        // attributed to the absent person.
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new OnValidationException([ResourcesErrorMessages.SESSION_MANAGER_CUSTODIAN_REQUIRES_REASON]);

        var custodian = await operatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(custodianId, actor.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_CUSTODIAN_NOT_FOUND);
        var grant = await operatorAccountsRepository.GetByOperatorIdAndAccountId(custodian.Id, command.Cashbox.Id);
        if (grant is not { Active: true })
            throw new ConflictException(ResourcesErrorMessages.SESSION_CUSTODIAN_NOT_GRANTED);

        return (custodian, SessionOpeningEvidenceKind.ManagerException);
    }

    private async Task<Dictionary<Guid, decimal>> ResolveAcknowledgedVector(
        Guid branchId,
        SessionStartCommand command,
        SessionOpeningProposal proposal,
        CancellationToken ct)
    {
        var items = command.Items ?? [];
        if (items.Select(item => item.ProductId).Distinct().Count() != items.Count)
            throw new OnValidationException([ResourcesErrorMessages.SESSION_OPENING_ITEM_DUPLICATE]);
        if (items.Any(item => item.Value < 0m))
            throw new OnValidationException([ResourcesErrorMessages.SESSION_OPENING_VALUE_NEGATIVE]);
        if (items.Any(item => decimal.Round(item.Value, 2) != item.Value))
            throw new OnValidationException([ResourcesErrorMessages.DAILYCLOSE_ITEM_VALUE_PRECISION]);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);
        var productIds = items.Select(item => item.ProductId).ToList();
        if (productIds.Contains(cashVarianceProductId))
            throw new OnValidationException([ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN]);
        if (productIds.Count > 0)
        {
            var products = await productsRepository.ListActiveByIdsAndBranchIdAsNoTracking(productIds, branchId, ct);
            if (products.Count != productIds.Count)
                throw new NotFoundException(ResourcesErrorMessages.SESSION_OPENING_PRODUCT_NOT_FOUND);
        }

        var acknowledged = items.ToDictionary(item => item.ProductId, item => item.Value);

        if (proposal.IsMissing)
        {
            if (command.ExplicitZero is false || acknowledged.Values.Any(value => value != 0m))
                throw new ConflictException(ResourcesErrorMessages.SESSION_OPENING_SOURCE_MISSING);
            return acknowledged;
        }

        if (proposal.Values.Keys.Any(productId => acknowledged.ContainsKey(productId) is false))
            throw new OnValidationException([ResourcesErrorMessages.SESSION_OPENING_ITEMS_INCOMPLETE]);

        if (acknowledged.Values.All(value => value == 0m) && command.ExplicitZero is false)
            throw new ConflictException(ResourcesErrorMessages.SESSION_OPENING_ZERO_NOT_ACKNOWLEDGED);

        return acknowledged;
    }
}
