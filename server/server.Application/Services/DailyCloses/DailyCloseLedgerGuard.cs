using server.Application.Services.Sessions;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.DailyCloses;

public sealed class DailyCloseLedgerGuard(
    IDailyClosesRepository dailyClosesRepository,
    ITransactionsRepository transactionsRepository,
    IAccountsRepository accountsRepository,
    ISessionHandoversRepository sessionHandoversRepository,
    SessionStarter sessionStarter,
    SessionHandoverAcceptor sessionHandoverAcceptor,
    IBranchClock branchClock) : IDailyCloseLedgerGuard
{
    public async Task<SessionRowResolution> ResolveSessionForNewRow(
        Guid accountId,
        AccountType accountType,
        DateTime date,
        SessionRowContext context,
        CancellationToken ct = default)
    {
        if (accountType != AccountType.Terminal)
            return SessionRowResolution.None;

        var actor = context.Actor;
        var branchId = actor.BranchId;
        var rowDate = date.Date;
        var isManager = actor.Role is Role.Manager or Role.Admin;

        if (context.RequestedSessionId is { } requestedSessionId)
        {
            // Historical writes name their exact session and a reason; they never open custody.
            if (isManager is false)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_NOT_ALLOWED);
            if (string.IsNullOrWhiteSpace(context.HistoricalReason))
                throw new OnValidationException([ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_REQUIRES_REASON]);
            SessionStarter.EnsureReasonLength(context.HistoricalReason);
            if (context.Acknowledgment is not null)
                throw new OnValidationException([ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_INVALID]);

            var named = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(requestedSessionId, branchId, ct)
                ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
            if (named.AccountId != accountId)
                throw new ConflictException(ResourcesErrorMessages.SESSION_NOT_FOR_ACCOUNT);
            if (named.MappingState != SessionMappingState.Ready)
                throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE);
            if (named.Date != rowDate)
                throw new ConflictException(ResourcesErrorMessages.SESSION_HISTORICAL_DATE_MISMATCH);
            if (named.Status != DailyCloseStatus.Draft)
                throw new ConflictException(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);

            return new SessionRowResolution(named, null);
        }

        var today = branchClock.LocalBusinessDate(branchClock.UtcNow());
        var active = await dailyClosesRepository.GetActiveCustodyByAccountIdAsNoTracking(branchId, accountId, ct);

        if (active is null)
        {
            if (rowDate != today)
                throw new ConflictException(ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_REQUIRES_SESSION);
            if (context.Acknowledgment is not { } acknowledgment)
                throw new ConflictException(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_REQUIRED);
            if (context.Preview)
                return SessionRowResolution.None;

            var started = await StartFromAcknowledgment(accountId, acknowledgment, context, ct);
            return new SessionRowResolution(started.Session, started);
        }

        if (context.Acknowledgment is { } pendingAcceptance)
        {
            // A pending handover leaves the outgoing session in custody until acceptance: the incoming operator's
            // first operation may accept it atomically. Any other acknowledgment over active custody (a lost
            // response retried with a new key, or a competing start) never opens a second custody.
            if (pendingAcceptance.HandoverId is { } handoverId
                && await IsPendingHandoverOf(active, handoverId, branchId, ct))
            {
                if (rowDate != today)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_REQUIRES_SESSION);
                if (context.Preview)
                    return SessionRowResolution.None;

                var accepted = await StartFromAcknowledgment(accountId, pendingAcceptance, context, ct);
                return new SessionRowResolution(accepted.Session, accepted);
            }

            throw new ConflictException(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_NOT_ALLOWED);
        }

        if (active.Date != rowDate)
        {
            throw new ConflictException(rowDate == today
                ? ResourcesErrorMessages.SESSION_CUSTODY_PREVIOUS_DAY
                : ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_REQUIRES_SESSION);
        }

        if (isManager is false && context.Scope.LinkedOperator?.Id != active.CustodianOperatorId)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.SESSION_NOT_CUSTODIAN);

        if (active.Status != DailyCloseStatus.Draft)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);

        return new SessionRowResolution(active, null);
    }

    public async Task EnsureLedgerIsMutable(Transaction row, CancellationToken ct = default)
    {
        if (row.SessionId is { } sessionId)
        {
            var session = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, row.BranchId, ct);
            if (session is not null && session.Status != DailyCloseStatus.Draft)
                throw new ConflictException(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
            return;
        }

        // A cashbox row without a session is unresolved legacy history: read-only until a reviewed mapping.
        var account = await accountsRepository.GetByIdAndBranchIdAsNoTracking(row.AccountId, row.BranchId, ct);
        if (account?.Type == AccountType.Terminal)
            throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE);
    }

    public async Task EnsureNoOutstandingDraftTransactions(DailyClose session, CancellationToken ct = default)
    {
        if (await transactionsRepository.ExistsDraftBySessionAsNoTracking(session, ct))
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS);
    }

    private async Task<bool> IsPendingHandoverOf(DailyClose active, Guid handoverId, Guid branchId, CancellationToken ct)
    {
        var pending = await sessionHandoversRepository.GetUnresolvedByOutgoingSessionId(branchId, active.Id, ct);
        return pending?.Id == handoverId;
    }

    private async Task<SessionStartResult> StartFromAcknowledgment(
        Guid accountId,
        Communication.Requests.RequestSessionOpeningAcknowledgmentJson acknowledgment,
        SessionRowContext context,
        CancellationToken ct)
    {
        var actor = context.Actor;
        var cashbox = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(accountId, actor.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        if (acknowledgment.HandoverId is { } handoverId)
        {
            var handover = await sessionHandoversRepository.GetByIdAndBranchId(handoverId, actor.BranchId, ct)
                ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_FOUND);
            if (handover.AccountId != accountId)
                throw new ConflictException(ResourcesErrorMessages.SESSION_NOT_FOR_ACCOUNT);

            var accepted = await sessionHandoverAcceptor.Accept(
                handover,
                acknowledgment.HandoverVersion,
                actor,
                context.Scope,
                acknowledgment.Items,
                acknowledgment.ExplicitZero,
                acknowledgment.Reason,
                ct);
            return accepted.Incoming;
        }

        return await sessionStarter.Start(new SessionStartCommand(
            cashbox,
            actor,
            context.Scope,
            acknowledgment.CustodianOperatorId,
            SessionOrigin.Started,
            acknowledgment.ExplicitZero,
            acknowledgment.Items,
            acknowledgment.Reason,
            acknowledgment.SourceKind,
            acknowledgment.SourceId,
            acknowledgment.SourceRevision), ct);
    }
}
