using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Application.Services.Receivables;

/// <summary>
/// Projects receivable rows from their events at one cutoff (M9.7 Phase 1) and maps them, with every event and both
/// dates, to the response shared by <c>GET /receivable/{id}</c> and every cheque command. Reads only.
/// </summary>
public class ReceivableResponseBuilder(
    ISettlementEventsRepository settlementEventsRepository,
    IBankReceiptsRepository bankReceiptsRepository)
{
    /// <summary>
    /// A cheque is an installment plan row or a row posted as <c>Cheque</c>. Once its lifecycle has command events, the
    /// instrument those events froze is its identity, so a later snapshot change can never strand a deposit or return.
    /// </summary>
    public static bool IsCheque(Transaction row, IReadOnlyCollection<SettlementEvent> events)
    {
        if (row.OriginTransactionId is not null)
            return true;

        var first = events.Where(e => e.Provenance == SettlementEventProvenance.Command).MinBy(e => e.Sequence);
        return (first?.SettlementInstrument ?? row.PostedSettlementInstrument) == SettlementInstrument.Cheque;
    }

    public static ReceivableProjection Project(Transaction row, IReadOnlyCollection<SettlementEvent> events, ReceivableCutoff cutoff) =>
        ReceivableProjector.Project(row.Value, row.AccountId, events.Select(ToFact).ToList(), cutoff.Basis, cutoff.Date);

    public static SettlementEventFact ToFact(SettlementEvent settlementEvent) => new(
        settlementEvent.Sequence,
        settlementEvent.Kind,
        settlementEvent.Amount,
        settlementEvent.OutstandingDelta,
        settlementEvent.OccurredOn,
        settlementEvent.PostedOn,
        settlementEvent.CustodyToAccountId,
        settlementEvent.Id);

    public async Task<IReadOnlyList<ResponseReceivableJson>> Build(
        IReadOnlyList<Transaction> rows,
        Guid branchId,
        ReceivableCutoff cutoff,
        CancellationToken ct = default)
    {
        var events = await settlementEventsRepository.ListByReceivableIdsAndBranchIdAsNoTracking(
            rows.Select(row => row.Id).ToList(), branchId, ct);
        var eventsByReceivable = events.ToLookup(e => e.ReceivableTransactionId);
        return rows.Select(row => Map(row, eventsByReceivable[row.Id].ToList(), cutoff)).ToList();
    }

    public static ResponseReceivableJson Map(Transaction row, IReadOnlyList<SettlementEvent> events, ReceivableCutoff cutoff)
    {
        var projection = Project(row, events, cutoff);
        return new ResponseReceivableJson
        {
            TransactionId = row.Id,
            OriginTransactionId = row.OriginTransactionId,
            AccountId = row.AccountId,
            ClientId = row.ClientId,
            Date = row.Date,
            DueDate = row.DueDate,
            Value = row.Value,
            SettlementInstrument = row.PostedSettlementInstrument ?? SettlementInstrument.None,
            IsCheque = IsCheque(row, events),
            CutoffBasis = cutoff.Basis,
            CutoffDate = cutoff.Date.Date,
            OutstandingValue = projection.OutstandingValue,
            SettledValue = projection.SettledValue,
            Status = projection.Status,
            ChequeState = projection.ChequeState,
            CustodyAccountId = projection.CustodyAccountId,
            CurrentDepositEventId = projection.CurrentDepositEventId,
            DepositAttempts = projection.DepositAttempts,
            Version = projection.Version,
            Events = events
                .OrderBy(e => e.Sequence)
                .Select(e => MapEvent(e, ReceivableProjector.BasisDate(ToFact(e), cutoff.Basis) <= cutoff.Date.Date))
                .ToList()
        };
    }

    public static ResponseSettlementEventJson MapEvent(SettlementEvent e, bool countsAtCutoff) => new()
    {
        Id = e.Id,
        Sequence = e.Sequence,
        Kind = e.Kind,
        Amount = e.Amount,
        OutstandingDelta = e.OutstandingDelta,
        OccurredOn = e.OccurredOn.Date,
        PostedOn = e.PostedOn.Date,
        IsLatePosting = e.PostedOn.Date > e.OccurredOn.Date,
        PostingLagDays = (e.PostedOn.Date - e.OccurredOn.Date).Days,
        CountsAtCutoff = countsAtCutoff,
        Provenance = e.Provenance,
        MigrationIssues = Enum.GetValues<SettlementMigrationIssue>()
            .Where(issue => issue != SettlementMigrationIssue.None && e.MigrationIssue.HasFlag(issue))
            .ToList(),
        SourcePaidAt = e.SourcePaidAt,
        SettlementInstrument = e.SettlementInstrument,
        BankReceiptId = e.BankReceiptId,
        ReversesEventId = e.ReversesEventId,
        CustodyFromAccountId = e.CustodyFromAccountId,
        CustodyToAccountId = e.CustodyToAccountId,
        ClientId = e.ClientId,
        NextExpectedDepositDate = e.NextExpectedDepositDate?.Date,
        ReturnReason = e.ReturnReason,
        ConvertedTransactionId = e.ConvertedTransactionId,
        CreatedByUserId = e.CreatedByUserId,
        CreatedAt = e.CreatedAt
    };

    /// <summary>
    /// A receipt with its allocations: the deposit events that claim it and the returns that took amounts back out.
    /// <c>ExpectedAmount</c> is what statement reconciliation may confirm.
    /// </summary>
    public async Task<ResponseBankReceiptJson?> BuildReceipt(Guid receiptId, Guid branchId, CancellationToken ct = default)
    {
        var receipt = await bankReceiptsRepository.GetByIdAndBranchIdAsNoTracking(receiptId, branchId, ct);
        if (receipt is null)
            return null;

        var deposits = await settlementEventsRepository.ListByBankReceiptIdAndBranchIdAsNoTracking(receipt.Id, branchId, ct);
        var returns = await settlementEventsRepository.ListReturnsOfDepositEventsAsNoTracking(
            deposits.Select(d => d.Id).ToList(), branchId, ct);
        var returnByDeposit = returns.ToDictionary(r => r.ReversesEventId!.Value, r => r.Id);
        var returnedAmount = deposits.Where(d => returnByDeposit.ContainsKey(d.Id)).Sum(d => d.Amount);

        return new ResponseBankReceiptJson
        {
            Id = receipt.Id,
            BankAccountId = receipt.BankAccountId,
            BankAccountName = receipt.BankAccount.Name,
            SourceKind = receipt.SourceKind,
            Status = receipt.Status,
            OccurredOn = receipt.OccurredOn.Date,
            PostedOn = receipt.PostedOn.Date,
            Amount = receipt.Amount,
            ReturnedAmount = returnedAmount,
            ExpectedAmount = receipt.Amount - returnedAmount,
            Description = receipt.Description,
            CreatedByUserId = receipt.CreatedByUserId,
            CreatedAt = receipt.CreatedAt,
            Allocations = deposits
                .Select(d => new ResponseBankReceiptAllocationJson
                {
                    SettlementEventId = d.Id,
                    TransactionId = d.ReceivableTransactionId,
                    Amount = d.Amount,
                    ReturnedByEventId = returnByDeposit.TryGetValue(d.Id, out var returnId) ? returnId : null
                })
                .ToList()
        };
    }
}
