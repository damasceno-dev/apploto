using server.Domain.Entities.Enums;

namespace server.Domain.Models;

/// <summary>The fields of a settlement event the projection reads.</summary>
public sealed record SettlementEventFact(
    int Sequence,
    SettlementEventKind Kind,
    decimal Amount,
    decimal OutstandingDelta,
    DateTime OccurredOn,
    DateTime PostedOn,
    Guid? CustodyToAccountId,
    Guid EventId);

/// <summary>A receivable's state at one cutoff, projected from its events (never stored).</summary>
public sealed record ReceivableProjection(
    ReceivableCutoffBasis Basis,
    DateTime CutoffDate,
    decimal Value,
    decimal OutstandingValue,
    decimal SettledValue,
    ReceivableSettlementStatus Status,
    ChequeLifecycleState ChequeState,
    Guid? CustodyAccountId,
    int Version,
    int CountedEventCount,
    int DepositAttempts,
    Guid? CurrentDepositEventId);

/// <summary>
/// The one receivable projection (M9.7 Phase 1.1–1.2): outstanding = value + Σ <c>OutstandingDelta</c> of the events
/// whose basis date is on or before the cutoff. Event chronology is enforced on both dates when an event is appended,
/// so the counted events are always a prefix of the chain on either basis. The SQL readers use the same rule.
/// </summary>
public static class ReceivableProjector
{
    public static DateTime BasisDate(SettlementEventFact fact, ReceivableCutoffBasis basis) =>
        basis == ReceivableCutoffBasis.Occurred ? fact.OccurredOn.Date : fact.PostedOn.Date;

    public static ReceivableProjection Project(
        decimal value,
        Guid recordingAccountId,
        IReadOnlyCollection<SettlementEventFact> events,
        ReceivableCutoffBasis basis,
        DateTime cutoffDate)
    {
        var ordered = events.OrderBy(fact => fact.Sequence).ToList();
        var counted = ordered.Where(fact => BasisDate(fact, basis) <= cutoffDate.Date).ToList();

        var outstanding = value + counted.Sum(fact => fact.OutstandingDelta);
        var status = outstanding == value
            ? ReceivableSettlementStatus.Open
            : outstanding == 0m
                ? ReceivableSettlementStatus.Settled
                : ReceivableSettlementStatus.PartiallySettled;

        var state = ChequeLifecycleState.Received;
        Guid? custody = recordingAccountId;
        var attempts = 0;
        Guid? currentDeposit = null;
        foreach (var fact in counted)
        {
            switch (fact.Kind)
            {
                case SettlementEventKind.MigratedSettlement:
                    if (fact.OutstandingDelta != 0m)
                    {
                        state = ChequeLifecycleState.SettledBeforeEvents;
                        custody = null;
                    }
                    break;
                case SettlementEventKind.CustodyMoved:
                    state = ChequeLifecycleState.InSafe;
                    custody = fact.CustodyToAccountId;
                    break;
                case SettlementEventKind.ChequeDeposited:
                    state = ChequeLifecycleState.Deposited;
                    custody = null;
                    attempts++;
                    currentDeposit = fact.EventId;
                    break;
                case SettlementEventKind.ChequeReturned:
                    state = ChequeLifecycleState.Returned;
                    custody = null;
                    currentDeposit = null;
                    break;
                case SettlementEventKind.ChequeRepresented:
                    state = ChequeLifecycleState.AwaitingRedeposit;
                    break;
                case SettlementEventKind.ChequeConvertedToFiado:
                    state = ChequeLifecycleState.ConvertedToFiado;
                    custody = null;
                    break;
            }
        }

        return new ReceivableProjection(
            basis,
            cutoffDate.Date,
            value,
            outstanding,
            value - outstanding,
            status,
            state,
            custody,
            ordered.Count == 0 ? 0 : ordered[^1].Sequence,
            counted.Count,
            attempts,
            currentDeposit);
    }
}

/// <summary>
/// Which cheque command each state admits (M9.7 Phase 2). A cheque is indivisible: deposit and conversion always take
/// the whole outstanding amount, which must still equal the face value.
/// </summary>
public static class ChequeLifecycleRules
{
    public static bool CanMoveToSafe(ChequeLifecycleState state) => state is
        ChequeLifecycleState.Received or
        ChequeLifecycleState.InSafe or
        ChequeLifecycleState.Returned or
        ChequeLifecycleState.AwaitingRedeposit;

    public static bool CanDeposit(ChequeLifecycleState state) => state is
        ChequeLifecycleState.Received or
        ChequeLifecycleState.InSafe or
        ChequeLifecycleState.AwaitingRedeposit;

    public static bool CanReturn(ChequeLifecycleState state) => state == ChequeLifecycleState.Deposited;

    public static bool CanRepresent(ChequeLifecycleState state) => state == ChequeLifecycleState.Returned;

    public static bool CanConvertToFiado(ChequeLifecycleState state) => state is
        ChequeLifecycleState.Received or
        ChequeLifecycleState.InSafe or
        ChequeLifecycleState.Returned or
        ChequeLifecycleState.AwaitingRedeposit;

    /// <summary>The persisted outstanding effect a kind must carry for an amount (mirrored by a database check).</summary>
    public static decimal OutstandingDeltaOf(SettlementEventKind kind, decimal amount) => kind switch
    {
        SettlementEventKind.MigratedSettlement or
        SettlementEventKind.ChequeDeposited or
        SettlementEventKind.ChequeConvertedToFiado => -amount,
        SettlementEventKind.ChequeReturned => amount,
        SettlementEventKind.CustodyMoved or
        SettlementEventKind.ChequeRepresented => 0m,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
