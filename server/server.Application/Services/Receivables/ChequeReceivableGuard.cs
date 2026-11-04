using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Receivables;

/// <summary>A cheque receivable read inside the command's coordination, with its whole event chain.</summary>
public sealed record ChequeReceivable(Transaction Row, IReadOnlyList<SettlementEvent> Events, ReceivableProjection Current)
{
    public SettlementEvent? Last => Events.Count == 0 ? null : Events[^1];

    /// <summary>
    /// The identified client. Once a return identified one, that immutable event wins over the row's editable
    /// <c>ClientId</c>; before any return it is the row's own client.
    /// </summary>
    public Guid? IdentifiedClientId =>
        Events.LastOrDefault(e => e.Kind == SettlementEventKind.ChequeReturned)?.ClientId ?? Row.ClientId;
}

/// <summary>
/// Loads and checks the cheques a command names (M9.7 Phase 2): each must be a branch row (404 otherwise, before any
/// data is returned), posted and Active, a cheque (an installment plan row or a row posted as <c>Cheque</c>), at the
/// version the client read, and whole — a cheque is indivisible, so its outstanding amount must still be its face value
/// unless the command moves the full remainder.
/// </summary>
public class ChequeReceivableGuard(
    ITransactionsRepository transactionsRepository,
    ISettlementEventsRepository settlementEventsRepository)
{
    /// <summary>The cheque rows' accounts, read before coordination so their keys can be locked.</summary>
    public async Task<IReadOnlyList<Guid>> ResolveAccountIds(
        IReadOnlyCollection<Guid> transactionIds,
        Guid branchId,
        CancellationToken ct = default)
    {
        var accountIds = new List<Guid>();
        foreach (var transactionId in transactionIds)
        {
            var row = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(transactionId, branchId, ct)
                ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
            accountIds.Add(row.AccountId);
        }

        return accountIds;
    }

    /// <summary>Re-reads each cheque and its chain inside the coordination and checks it against the expected version.</summary>
    public async Task<IReadOnlyList<ChequeReceivable>> Load(
        IReadOnlyList<(Guid TransactionId, int ExpectedVersion)> items,
        Guid branchId,
        DateTime today,
        CancellationToken ct = default)
    {
        var rows = new List<Transaction>();
        foreach (var (transactionId, _) in items)
        {
            var row = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(transactionId, branchId, ct)
                ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
            rows.Add(row);
        }

        var events = await settlementEventsRepository.ListByReceivableIdsAndBranchIdAsNoTracking(
            rows.Select(row => row.Id).ToList(), branchId, ct);
        var eventsByReceivable = events.ToLookup(e => e.ReceivableTransactionId);

        var cheques = new List<ChequeReceivable>();
        foreach (var (row, (_, expectedVersion)) in rows.Zip(items))
        {
            // A Draft is not posted yet, so it has no frozen instrument: report it as not posted rather than not a cheque.
            if (row.Active is false || row.Status != TransactionStatus.Active)
                throw new ConflictException(ResourcesErrorMessages.CHEQUE_NOT_ACTIVE);
            var chain = eventsByReceivable[row.Id].OrderBy(e => e.Sequence).ToList();
            if (row.GenerationKind is not null || ReceivableResponseBuilder.IsCheque(row, chain) is false)
                throw new ConflictException(ResourcesErrorMessages.CHEQUE_NOT_A_CHEQUE);

            var current = ReceivableResponseBuilder.Project(
                row, chain, new ReceivableCutoff(ReceivableCutoffBasis.Posted, today));
            if (current.Version != expectedVersion)
                throw new ConflictException(ResourcesErrorMessages.CHEQUE_STALE_VERSION);

            cheques.Add(new ChequeReceivable(row, chain, current));
        }

        return cheques;
    }

    public static void EnsureWhole(ChequeReceivable cheque)
    {
        if (cheque.Current.OutstandingValue != cheque.Row.Value)
            throw new ConflictException(ResourcesErrorMessages.CHEQUE_PARTIAL_AMOUNT_UNSUPPORTED);
    }

    /// <summary>The next event of the chain, linked to its predecessor, with the receivable's frozen instrument.</summary>
    public static SettlementEvent NextEvent(
        ChequeReceivable cheque,
        SettlementEventKind kind,
        decimal amount,
        SettlementEventDates dates,
        Guid userId,
        SettlementEventDetails? details = null)
    {
        details ??= new SettlementEventDetails();
        return new SettlementEvent
        {
            BranchId = cheque.Row.BranchId,
            ReceivableTransactionId = cheque.Row.Id,
            Sequence = (cheque.Last?.Sequence ?? 0) + 1,
            PreviousEventId = cheque.Last?.Id,
            Kind = kind,
            Amount = amount,
            OutstandingDelta = ChequeLifecycleRules.OutstandingDeltaOf(kind, amount),
            OccurredOn = dates.OccurredOn,
            PostedOn = dates.PostedOn,
            Provenance = SettlementEventProvenance.Command,
            MigrationIssue = SettlementMigrationIssue.None,
            SettlementInstrument = cheque.Events.Where(e => e.Provenance == SettlementEventProvenance.Command)
                .MinBy(e => e.Sequence)?.SettlementInstrument
                ?? cheque.Row.PostedSettlementInstrument ?? SettlementInstrument.None,
            CreatedByUserId = userId,
            BankReceiptId = details.BankReceiptId,
            ReversesEventId = details.ReversesEventId,
            CustodyFromAccountId = details.CustodyFromAccountId,
            CustodyToAccountId = details.CustodyToAccountId,
            ClientId = details.ClientId,
            NextExpectedDepositDate = details.NextExpectedDepositDate,
            ReturnReason = details.ReturnReason,
            ConvertedTransactionId = details.ConvertedTransactionId
        };
    }
}

/// <summary>The kind-specific fields of a new event (the database requires exactly those of its kind).</summary>
public sealed record SettlementEventDetails
{
    public Guid? BankReceiptId { get; init; }
    public Guid? ReversesEventId { get; init; }
    public Guid? CustodyFromAccountId { get; init; }
    public Guid? CustodyToAccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime? NextExpectedDepositDate { get; init; }
    public string? ReturnReason { get; init; }
    public Guid? ConvertedTransactionId { get; init; }
}
