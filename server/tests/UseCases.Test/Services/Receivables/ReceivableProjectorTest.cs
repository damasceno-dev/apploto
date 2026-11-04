using server.Domain.Entities.Enums;
using server.Domain.Models;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Receivables;

/// <summary>
/// M9.7 Phase 1.1–1.2: the pure projection every reader shares. Generic partial events here are model fixtures — no
/// public cheque operation settles part of a cheque.
/// </summary>
public class ReceivableProjectorTest
{
    private static readonly Guid Cashbox = Guid.NewGuid();
    private static readonly Guid Safe = Guid.NewGuid();

    private static SettlementEventFact Fact(int sequence, SettlementEventKind kind, decimal amount, DateTime occurred, DateTime posted,
        Guid? custodyTo = null) =>
        new(sequence, kind, amount, ChequeLifecycleRules.OutstandingDeltaOf(kind, amount), occurred, posted, custodyTo, Guid.NewGuid());

    [Fact]
    public void Project_ShouldCountOnlyEventsOnOrBeforeTheCutoff_OnTheChosenBasis()
    {
        // E13a: deposited on 30/09 (locked), posted on 02/10.
        var deposit = Fact(1, SettlementEventKind.ChequeDeposited, 200m, new DateTime(2025, 9, 30), new DateTime(2025, 10, 2));

        var posted = ReceivableProjector.Project(200m, Cashbox, [deposit], ReceivableCutoffBasis.Posted, new DateTime(2025, 9, 30));
        var occurred = ReceivableProjector.Project(200m, Cashbox, [deposit], ReceivableCutoffBasis.Occurred, new DateTime(2025, 9, 30));
        var later = ReceivableProjector.Project(200m, Cashbox, [deposit], ReceivableCutoffBasis.Posted, new DateTime(2025, 10, 2));

        (posted.OutstandingValue, posted.Status, posted.ChequeState, posted.CountedEventCount, posted.Version)
            .ShouldBe((200m, ReceivableSettlementStatus.Open, ChequeLifecycleState.Received, 0, 1));
        (occurred.OutstandingValue, occurred.Status, occurred.ChequeState)
            .ShouldBe((0m, ReceivableSettlementStatus.Settled, ChequeLifecycleState.Deposited));
        (later.OutstandingValue, later.SettledValue, later.DepositAttempts).ShouldBe((0m, 200m, 1));
    }

    [Fact]
    public void Project_ShouldLeaveAVisibleRemainder_ForAGenericPartialSettlement()
    {
        // Model fixture: a migrated partial settlement of 120 on a 200 receivable (the future card-batch shape, E13b).
        var partial = Fact(1, SettlementEventKind.MigratedSettlement, 120m, new DateTime(2025, 9, 30), new DateTime(2025, 10, 2));

        var projection = ReceivableProjector.Project(200m, Cashbox, [partial], ReceivableCutoffBasis.Occurred, new DateTime(2025, 9, 30));

        (projection.OutstandingValue, projection.SettledValue, projection.Status)
            .ShouldBe((80m, 120m, ReceivableSettlementStatus.PartiallySettled));
        ReceivableProjector.Project(200m, Cashbox, [partial], ReceivableCutoffBasis.Posted, new DateTime(2025, 9, 30))
            .OutstandingValue.ShouldBe(200m);
    }

    [Fact]
    public void Project_ShouldFollowTheChequeChain_ThroughCustodyReturnRepresentationAndRedeposit()
    {
        var day = new DateTime(2025, 8, 1);
        var chain = new[]
        {
            Fact(1, SettlementEventKind.CustodyMoved, 150m, day, day, Safe),
            Fact(2, SettlementEventKind.ChequeDeposited, 150m, day.AddDays(1), day.AddDays(1)),
            Fact(3, SettlementEventKind.ChequeReturned, 150m, day.AddDays(5), day.AddDays(6)),
            Fact(4, SettlementEventKind.ChequeRepresented, 150m, day.AddDays(7), day.AddDays(7)),
            Fact(5, SettlementEventKind.ChequeDeposited, 150m, day.AddDays(8), day.AddDays(8))
        };

        ChequeLifecycleState At(int offset) =>
            ReceivableProjector.Project(150m, Cashbox, chain, ReceivableCutoffBasis.Posted, day.AddDays(offset)).ChequeState;

        ReceivableProjector.Project(150m, Cashbox, [], ReceivableCutoffBasis.Posted, day).CustodyAccountId.ShouldBe(Cashbox);
        ReceivableProjector.Project(150m, Cashbox, chain, ReceivableCutoffBasis.Posted, day).CustodyAccountId.ShouldBe(Safe);
        (At(0), At(1), At(5), At(6), At(7), At(8)).ShouldBe((
            ChequeLifecycleState.InSafe, ChequeLifecycleState.Deposited, ChequeLifecycleState.Deposited,
            ChequeLifecycleState.Returned, ChequeLifecycleState.AwaitingRedeposit, ChequeLifecycleState.Deposited));
        var final = ReceivableProjector.Project(150m, Cashbox, chain, ReceivableCutoffBasis.Posted, day.AddDays(8));
        (final.OutstandingValue, final.DepositAttempts, final.Version, final.CurrentDepositEventId)
            .ShouldBe((0m, 2, 5, (Guid?)chain[4].EventId));
    }

    [Fact]
    public void Project_ShouldTreatATabRowReconstructionAsNoSettlement()
    {
        var tab = new SettlementEventFact(1, SettlementEventKind.MigratedSettlement, 50m, 0m,
            new DateTime(2025, 1, 2), new DateTime(2025, 1, 2), null, Guid.NewGuid());

        var projection = ReceivableProjector.Project(50m, Cashbox, [tab], ReceivableCutoffBasis.Posted, new DateTime(2025, 2, 1));

        (projection.OutstandingValue, projection.ChequeState).ShouldBe((50m, ChequeLifecycleState.Received));
    }

    [Theory]
    [InlineData(SettlementEventKind.MigratedSettlement, -10)]
    [InlineData(SettlementEventKind.ChequeDeposited, -10)]
    [InlineData(SettlementEventKind.ChequeConvertedToFiado, -10)]
    [InlineData(SettlementEventKind.ChequeReturned, 10)]
    [InlineData(SettlementEventKind.CustodyMoved, 0)]
    [InlineData(SettlementEventKind.ChequeRepresented, 0)]
    public void OutstandingDelta_ShouldBeFixedByKind_AsTheDatabaseCheckRequires(SettlementEventKind kind, int expected)
    {
        ChequeLifecycleRules.OutstandingDeltaOf(kind, 10m).ShouldBe(expected);
    }

    [Fact]
    public void LifecycleRules_ShouldAdmitEachCommandOnlyFromItsLegalStates()
    {
        var states = Enum.GetValues<ChequeLifecycleState>();
        states.Where(ChequeLifecycleRules.CanDeposit).ShouldBe([
            ChequeLifecycleState.Received, ChequeLifecycleState.InSafe, ChequeLifecycleState.AwaitingRedeposit]);
        states.Where(ChequeLifecycleRules.CanReturn).ShouldBe([ChequeLifecycleState.Deposited]);
        states.Where(ChequeLifecycleRules.CanRepresent).ShouldBe([ChequeLifecycleState.Returned]);
        states.Where(ChequeLifecycleRules.CanConvertToFiado).ShouldBe([
            ChequeLifecycleState.Received, ChequeLifecycleState.InSafe, ChequeLifecycleState.Returned, ChequeLifecycleState.AwaitingRedeposit]);
        states.Where(ChequeLifecycleRules.CanMoveToSafe).ShouldBe([
            ChequeLifecycleState.Received, ChequeLifecycleState.InSafe, ChequeLifecycleState.Returned, ChequeLifecycleState.AwaitingRedeposit]);
    }
}
