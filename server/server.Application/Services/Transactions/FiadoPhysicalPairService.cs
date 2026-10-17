using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

/// <summary>
/// Builds the generated Terminal leg of a physical fiado operation (M7.7 Phase 5.6, decision 1.3 amendment (i)):
/// a fiado sale that hands out counted stock writes a Terminal <c>Out</c>, a counted-cash repayment a Terminal
/// <c>In</c>, both on the Tab's paired terminal and with the source's value, date, status and recording identity.
/// The pairing names the routing target only; a Member still needs that terminal in their own explicit scope
/// (decision 1.8). Callers hold one coordination scope over both accounts and write both rows in one commit.
/// </summary>
public class FiadoPhysicalPairService(
    IAccountsRepository accountsRepository,
    ITransactionTypesRepository transactionTypesRepository,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    MemberAccountScopeGuard memberAccountScopeGuard)
{
    /// <summary>The Tab's active paired terminal, or 409 when the Tab has none.</summary>
    public async Task<Guid> ResolvePairedTerminalId(Guid branchId, Guid tabAccountId)
    {
        return await accountsRepository.GetActiveTerminalIdByTabAccountId(tabAccountId, branchId)
            ?? throw new ConflictException(ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED);
    }

    /// <summary>
    /// Inside the coordination scope: re-reads the pairing (it must still name the terminal that was locked),
    /// enforces the Member's own terminal scope and the terminal's open Draft close for the date, and returns the
    /// unsaved generated row linked to <paramref name="source"/>.
    /// </summary>
    public async Task<Transaction> BuildCounterpart(
        Transaction source,
        Guid lockedTerminalId,
        TransactionGenerationKind kind,
        Role callerRole,
        MemberAccountScope memberScope,
        CancellationToken ct = default)
    {
        var terminalId = await ValidateCounterpart(
            source.BranchId,
            source.AccountId,
            source.Date,
            callerRole,
            memberScope,
            lockedTerminalId,
            ct);

        var role = kind == TransactionGenerationKind.FiadoCountedStockSale
            ? TransactionTypeSystemRole.FiadoCountedStockSaleCounterpart
            : TransactionTypeSystemRole.FiadoCashRepaymentCounterpart;
        var systemType = await transactionTypesRepository.GetActiveSystemTypeByBranchIdAsNoTracking(source.BranchId, role, ct)
            ?? throw new InvalidOperationException($"Branch {source.BranchId} has no active system type {role}.");

        return new Transaction
        {
            Date = source.Date,
            Value = source.Value,
            TransactionTime = source.TransactionTime,
            TransactionTypeId = systemType.Id,
            CategoryId = systemType.CategoryId,
            Direction = systemType.Category.DefaultDirection,
            AccountId = terminalId,
            DueDate = source.Date,
            RecordedByOperatorId = source.RecordedByOperatorId,
            CreatedByUserId = source.CreatedByUserId,
            Status = source.Status,
            BranchId = source.BranchId,
            GenerationKind = kind,
            GeneratedFromTransactionId = source.Id
        };
    }

    /// <summary>
    /// Every check the generated leg needs, without building it — shared by the write (inside its coordination scope,
    /// with the terminal it locked) and by the create preview (no lock; <paramref name="lockedTerminalId"/> null):
    /// an active pairing, the Member's own terminal scope, and the terminal's open Draft close for the date.
    /// Returns the paired terminal id.
    /// </summary>
    public async Task<Guid> ValidateCounterpart(
        Guid branchId,
        Guid tabAccountId,
        DateTime date,
        Role callerRole,
        MemberAccountScope memberScope,
        Guid? lockedTerminalId,
        CancellationToken ct = default)
    {
        var terminalId = await ResolvePairedTerminalId(branchId, tabAccountId);
        if (lockedTerminalId is { } locked && terminalId != locked)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED);

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(callerRole, memberScope, terminalId);

        await dailyCloseLedgerGuard.EnsureLedgerAcceptsNewRow(branchId, terminalId, AccountType.Terminal, date.Date, ct);
        return terminalId;
    }

    /// <summary>
    /// For finalize/cancel of a source: the counterpart moves with it, so the terminal's ledger must be mutable
    /// and a Member must still hold the terminal in scope.
    /// </summary>
    public async Task EnsureCounterpartMutable(
        Transaction counterpart,
        Role callerRole,
        MemberAccountScope memberScope,
        CancellationToken ct = default)
    {
        if (callerRole == Role.Member && memberScope.LinkedOperator is not null)
            memberAccountScopeGuard.EnsureMemberCanActOnAccount(callerRole, memberScope, counterpart.AccountId);

        await dailyCloseLedgerGuard.EnsureLedgerIsMutable(
            counterpart.BranchId,
            counterpart.AccountId,
            counterpart.Date.Date,
            ct);
    }
}
