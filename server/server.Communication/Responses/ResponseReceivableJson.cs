using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// A receivable row projected from its events at one cutoff (M9.7 Phases 1–2). The row itself is never edited by
/// settlement: its status, outstanding amount and cheque state come only from the events counted at the cutoff.
/// </summary>
public class ResponseReceivableJson
{
    public Guid TransactionId { get; init; }
    public Guid? OriginTransactionId { get; init; }
    public Guid AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime Date { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }

    /// <summary>The instrument frozen when the row was posted; <c>None</c> for an unclassified installment row.</summary>
    public SettlementInstrument SettlementInstrument { get; init; }

    /// <summary>True for an installment plan row or a row posted as <c>Cheque</c>: the cheque commands accept it.</summary>
    public bool IsCheque { get; init; }

    public ReceivableCutoffBasis CutoffBasis { get; init; }
    public DateTime CutoffDate { get; init; }

    public decimal OutstandingValue { get; init; }
    public decimal SettledValue { get; init; }
    public ReceivableSettlementStatus Status { get; init; }

    /// <summary>The cheque's state at the cutoff; meaningful when <see cref="IsCheque"/>.</summary>
    public ChequeLifecycleState ChequeState { get; init; }

    /// <summary>The account holding the physical cheque, when known (the recording cashbox or a cofre).</summary>
    public Guid? CustodyAccountId { get; init; }

    /// <summary>The deposit event a return would reverse, when the cheque is deposited.</summary>
    public Guid? CurrentDepositEventId { get; init; }

    public int DepositAttempts { get; init; }

    /// <summary>
    /// The receivable version: its last event sequence (0 before any event), whatever the cutoff. Commands send it as
    /// <c>ExpectedVersion</c>.
    /// </summary>
    public int Version { get; init; }

    /// <summary>Every event of the chain in sequence order, each saying whether it counts at the cutoff.</summary>
    public IReadOnlyList<ResponseSettlementEventJson> Events { get; init; } = [];
}
