using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Sessions;

/// <summary>
/// Maintains the generated ledger effect of each close-form field (M9.6 Phase 7, §6.19 O08, §6.20 field-save
/// semantics). A field holds one role slot per session; a changed value cancels the slot's current row and appends the
/// replacement — never a negative <see cref="Transaction.Value"/>: a negative report is an <c>Out</c> of its
/// magnitude — and zero cancels without a replacement. Every change appends an immutable revision. The caller holds
/// the cashbox coordination, has checked edit authority, lock date and version, and commits once.
/// </summary>
public sealed class SessionFieldEffectService(
    ISessionFieldEffectsRepository fieldEffectsRepository,
    ITransactionTypesRepository transactionTypesRepository,
    ITransactionsRepository transactionsRepository)
{
    private static readonly SessionCloseField[] AllFields =
        [SessionCloseField.TerminalReport, SessionCloseField.BolaoFee, SessionCloseField.SpecialFee];

    /// <summary>
    /// Applies the listed fields to a Draft session. Returns the rows whose calculation contribution changed (created
    /// Active or cancelled), for the ledger revision; an unchanged value writes nothing.
    /// </summary>
    public async Task<IReadOnlyList<Transaction>> Apply(
        DailyClose session,
        IReadOnlyList<RequestSessionCloseFieldJson>? fields,
        BranchUser actor,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        if (fields is null || fields.Count == 0)
            return [];

        var slots = (await fieldEffectsRepository.ListBySessionId(session.BranchId, session.Id, ct))
            .ToDictionary(slot => slot.Field);
        var changed = new List<Transaction>();

        foreach (var field in fields.OrderBy(field => field.Field))
        {
            var value = field.Value!.Value;
            slots.TryGetValue(field.Field, out var slot);
            var previous = slot?.Value ?? 0m;
            if (value == previous)
                continue;

            if (await fieldEffectsRepository.ExistsUnadoptedFieldRowAsNoTracking(
                    session.BranchId, session.Id, RolesOf(field.Field), ct))
            {
                throw new ConflictException(ResourcesErrorMessages.SESSION_CLOSE_FIELD_LEGACY_ROWS_UNRESOLVED);
            }

            if (slot is null)
            {
                slot = new SessionFieldEffect
                {
                    BranchId = session.BranchId,
                    SessionId = session.Id,
                    Field = field.Field
                };
                await fieldEffectsRepository.Add(slot, ct);
                slots[field.Field] = slot;
            }

            Transaction? cancelled = null;
            if (slot.CurrentTransactionId is { } currentId)
            {
                cancelled = await transactionsRepository.GetByIdAndBranchId(currentId, session.BranchId, ct)
                    ?? throw new InvalidOperationException($"Field effect {currentId} of session {session.Id} is missing.");
                cancelled.Status = TransactionStatus.Cancelled;
                cancelled.CancelledAt = utcNow;
                cancelled.CancelledByUserId = actor.UserId;
                cancelled.CancellationReason = $"Campo do fechamento alterado (revisão {slot.Revision + 1})";
                changed.Add(cancelled);
            }

            Transaction? created = null;
            if (value != 0m)
            {
                created = await BuildEffect(session, field.Field, value, actor, ct);
                await transactionsRepository.Add(created, ct);
                changed.Add(created);
            }

            slot.Value = value;
            slot.Revision++;
            slot.CurrentTransactionId = created?.Id;
            slot.UpdatedAt = utcNow;
            slot.UpdatedByUserId = actor.UserId;

            await fieldEffectsRepository.AddRevision(new SessionFieldEffectRevision
            {
                SessionFieldEffectId = slot.Id,
                Revision = slot.Revision,
                PreviousValue = previous,
                Value = value,
                CreatedTransactionId = created?.Id,
                CancelledTransactionId = cancelled?.Id,
                Origin = SessionFieldEffectOrigin.Save,
                ActorUserId = actor.UserId,
                RecordedAt = utcNow
            }, ct);
        }

        return changed;
    }

    /// <summary>All three fields of a session, zero where never set; reads only.</summary>
    public async Task<IReadOnlyList<ResponseSessionCloseFieldJson>> Read(
        Guid branchId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        var slots = (await fieldEffectsRepository.ListBySessionIdAsNoTracking(branchId, sessionId, ct))
            .ToDictionary(slot => slot.Field);
        return AllFields
            .Select(field => slots.TryGetValue(field, out var slot)
                ? new ResponseSessionCloseFieldJson
                {
                    Field = field,
                    Value = slot.Value,
                    Revision = slot.Revision,
                    TransactionId = slot.CurrentTransactionId,
                    UpdatedAt = slot.UpdatedAt,
                    UpdatedByUserId = slot.UpdatedByUserId
                }
                : new ResponseSessionCloseFieldJson { Field = field })
            .ToList();
    }

    private async Task<Transaction> BuildEffect(
        DailyClose session,
        SessionCloseField field,
        decimal value,
        BranchUser actor,
        CancellationToken ct)
    {
        var role = FinancialRoleRules.SystemRoleOf(field, value);
        var type = await transactionTypesRepository.GetActiveSystemTypeByBranchIdAsNoTracking(session.BranchId, role, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.BRANCH_FINANCIAL_SEEDS_MISSING);

        return new Transaction
        {
            Date = session.Date,
            Value = Math.Abs(value),
            TransactionTypeId = type.Id,
            CategoryId = type.CategoryId,
            Direction = type.Category.DefaultDirection,
            AccountId = session.AccountId,
            DueDate = session.Date,
            // The session's custodian answers for its report and fees, whoever types them (§3.30 identity).
            RecordedByOperatorId = session.CustodianOperatorId
                ?? throw new InvalidOperationException($"Session {session.Id} has no custodian."),
            CreatedByUserId = actor.UserId,
            Status = TransactionStatus.Active,
            BranchId = session.BranchId,
            GenerationKind = FinancialRoleRules.GenerationKindOf(field),
            SessionId = session.Id
        };
    }

    private static TransactionTypeSystemRole[] RolesOf(SessionCloseField field) => field switch
    {
        SessionCloseField.TerminalReport => [TransactionTypeSystemRole.TerminalReportIn, TransactionTypeSystemRole.TerminalReportOut],
        SessionCloseField.BolaoFee => [TransactionTypeSystemRole.BolaoFee],
        SessionCloseField.SpecialFee => [TransactionTypeSystemRole.SpecialFee],
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };
}
