using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Cheques;

/// <summary>
/// M9.7 Phases 1–2 golden walkthroughs on real PostgreSQL (§6.19 E13a, O10, O11, O21): every figure below is derived by
/// hand from the scenario, then read back from persisted events, receipts and rows and from the reports at both cutoff
/// bases. A 2xx alone proves nothing here.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ChequeLifecycleGoldenTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task LockCrossingDepositReturnAndConversion_ShouldKeepLockedFiguresAndPostEachOutcomeOnce()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeE13a");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 10, 2, 10, 0, 0));
        var cheque = await s.SeedChequeAsync(new DateTime(2025, 9, 10), 200m, dueDate: new DateTime(2025, 9, 30));
        var original = (await factory.ReloadAsync<Transaction>(cheque.Id))!;
        (original.PostedSettlementInstrument, original.ClassificationProvenance, original.ClassificationRevision)
            .ShouldBe((SettlementInstrument.Cheque, (ClassificationProvenance?)ClassificationProvenance.Posting, 1));
        await s.LockThroughAsync(new DateTime(2025, 9, 30));
        var rowsBefore = await s.BranchTransactionCountAsync();

        // E13a: the full cheque is deposited on 30/09 (a locked day) and posted in open October (today, 02/10).
        var deposit = await s.DepositOkAsync(new DateTime(2025, 9, 30), (cheque.Id, 0));

        var deposited = deposit.Receivables.Single();
        (deposited.Status, deposited.ChequeState, deposited.OutstandingValue, deposited.SettledValue, deposited.Version)
            .ShouldBe((ReceivableSettlementStatus.Settled, ChequeLifecycleState.Deposited, 0m, 200m, 1));
        var depositEvent = deposited.Events.Single();
        (depositEvent.OccurredOn, depositEvent.PostedOn, depositEvent.IsLatePosting, depositEvent.PostingLagDays)
            .ShouldBe((new DateTime(2025, 9, 30), new DateTime(2025, 10, 2), true, 2));
        var receipt = deposit.BankReceipt!;
        (receipt.Status, receipt.Amount, receipt.ExpectedAmount, receipt.ReturnedAmount, receipt.BankAccountId)
            .ShouldBe((BankReceiptStatus.Pending, 200m, 200m, 0m, s.Bank.Id));
        receipt.Allocations.Single().SettlementEventId.ShouldBe(depositEvent.Id);

        // No ledger row, cashbox leg or second receipt; the locked original is byte-for-byte the same.
        (await s.BranchTransactionCountAsync()).ShouldBe(rowsBefore);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
        var untouched = (await factory.ReloadAsync<Transaction>(cheque.Id))!;
        (untouched.Version, untouched.Date, untouched.Value, untouched.Status, untouched.ClassificationRevision)
            .ShouldBe((original.Version, original.Date, original.Value, original.Status, original.ClassificationRevision));

        // Cutoffs: the posted September report is reproducible (still 200 open); the operational view shows the deposit.
        (await s.OpenChequesAsync(new DateTime(2025, 9, 30))).OutstandingTotal.ShouldBe(200m);
        (await s.OpenChequesAsync(new DateTime(2025, 9, 30), ReceivableCutoffBasis.Occurred)).OutstandingTotal.ShouldBe(0m);
        (await s.OpenChequesAsync(new DateTime(2025, 10, 2))).OutstandingTotal.ShouldBe(0m);
        var atLockedCutoff = await s.ReceivableAsync(cheque.Id, new DateTime(2025, 9, 30));
        (atLockedCutoff.OutstandingValue, atLockedCutoff.Events.Single().CountsAtCutoff, atLockedCutoff.Version)
            .ShouldBe((200m, false, 1));

        var originalResult = await _client.FinancialResultAsync(s.ManagerToken, new DateTime(2025, 10, 2), new DateTime(2025, 10, 2));
        originalResult.KnownOwnResult.ShouldBe(0m);
        originalResult.Classes.Single(c => c.FinancialClass == FinancialClass.InternalTransfer).KnownSignedTotal.ShouldBe(200m);

        // October locks; the bank returned the cheque on 15/10 (locked), recorded on 05/11.
        await s.LockThroughAsync(new DateTime(2025, 10, 31));
        clock.AdvanceTo(new DateTime(2025, 11, 5, 9, 0, 0));
        var intoLocked = await _client.PostAuthAsync(
            $"/cheque/{cheque.Id}/return",
            new RequestChequeReturnJson
            {
                DepositEventId = depositEvent.Id, ExpectedVersion = 1, OccurredOn = new DateTime(2025, 10, 15),
                PostedOn = new DateTime(2025, 10, 20), ClientId = s.Day.ClientId, NextExpectedDepositDate = new DateTime(2025, 11, 20)
            },
            s.ManagerToken);
        await ShouldFail(intoLocked, HttpStatusCode.Conflict, ResourcesErrorMessages.SETTLEMENT_POSTED_ON_LOCKED);

        var returnResponse = await s.ReturnAsync(cheque.Id, depositEvent.Id, 1, new DateTime(2025, 10, 15), s.Day.ClientId,
            nextDeposit: new DateTime(2025, 11, 20), reason: "11");
        returnResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await returnResponse.Content.ReadAsStringAsync());
        var returned = await returnResponse.ReadContentAsync<ResponseChequeOperationJson>();
        var reopened = returned.Receivables.Single();
        (reopened.Status, reopened.ChequeState, reopened.OutstandingValue, reopened.Version)
            .ShouldBe((ReceivableSettlementStatus.Open, ChequeLifecycleState.Returned, 200m, 2));
        var returnEvent = reopened.Events[1];
        (returnEvent.ReversesEventId, returnEvent.ClientId, returnEvent.ReturnReason, returnEvent.NextExpectedDepositDate)
            .ShouldBe(((Guid?)depositEvent.Id, (Guid?)s.Day.ClientId, "11", (DateTime?)new DateTime(2025, 11, 20)));
        // The exact pending receipt now expects nothing: reconciliation can never confirm the returned amount.
        (returned.BankReceipt!.Id, returned.BankReceipt.ExpectedAmount, returned.BankReceipt.ReturnedAmount)
            .ShouldBe((receipt.Id, 0m, 200m));
        returned.BankReceipt.Allocations.Single().ReturnedByEventId.ShouldBe(returnEvent.Id);

        // Locked October stays as it was on the posted basis; the operational view sees the return in October.
        (await s.OpenChequesAsync(new DateTime(2025, 10, 31))).OutstandingTotal.ShouldBe(0m);
        (await s.OpenChequesAsync(new DateTime(2025, 10, 31), ReceivableCutoffBasis.Occurred)).OutstandingTotal.ShouldBe(200m);
        (await s.OpenChequesAsync(new DateTime(2025, 11, 5))).OutstandingTotal.ShouldBe(200m);

        var twice = await s.ReturnAsync(cheque.Id, depositEvent.Id, 2, new DateTime(2025, 10, 16), s.Day.ClientId);
        await ShouldFail(twice, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_RETURN_STATE_INVALID);
        var otherClient = await factory.SeedClientAsync(s.BranchId, "Outro cliente");
        var wrongClient = await s.ConvertAsync(cheque.Id, 2, new DateTime(2025, 11, 5), otherClient.Id);
        await ShouldFail(wrongClient, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_CLIENT_MISMATCH);

        // Converter em fiado: the claim closes and the same client's debt opens, in one commit, with no cashbox leg.
        var cashboxRowsBefore = await s.QueryAsync(db => db.Transactions.CountAsync(t => t.AccountId == s.Day.CashboxId));
        var convertResponse = await s.ConvertAsync(cheque.Id, 2, new DateTime(2025, 11, 5), s.Day.ClientId);
        convertResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await convertResponse.Content.ReadAsStringAsync());
        var converted = await convertResponse.ReadContentAsync<ResponseChequeOperationJson>();
        var closed = converted.Receivables.Single();
        (closed.Status, closed.ChequeState, closed.OutstandingValue, closed.Version)
            .ShouldBe((ReceivableSettlementStatus.Settled, ChequeLifecycleState.ConvertedToFiado, 0m, 3));

        var debt = (await factory.ReloadAsync<Transaction>(converted.ConvertedTransactionId!.Value))!;
        (debt.AccountId, debt.Direction, debt.Value, debt.ClientId, debt.Date, debt.SessionId, debt.Status)
            .ShouldBe((s.Day.TabId, Direction.Out, 200m, (Guid?)s.Day.ClientId, new DateTime(2025, 11, 5), (Guid?)null, TransactionStatus.Active));
        (debt.GenerationKind, debt.GeneratedFromTransactionId, debt.TransactionTypeId)
            .ShouldBe(((TransactionGenerationKind?)TransactionGenerationKind.ChequeFiadoConversion, (Guid?)cheque.Id,
                s.Day.SystemTypes[TransactionTypeSystemRole.ChequeFiadoConversion].Id));
        debt.PostedFinancialClass.ShouldBe(FinancialClass.CaixaAndClientMoney);
        (await s.BranchTransactionCountAsync()).ShouldBe(rowsBefore + 1);
        (await s.QueryAsync(db => db.Transactions.CountAsync(t => t.AccountId == s.Day.CashboxId))).ShouldBe(cashboxRowsBefore);

        // One outstanding claim: the fiado debt, never also the cheque.
        (await s.FiadoBalanceAsync(s.Day.ClientId, new DateTime(2025, 11, 5))).ShouldBe(200m);
        (await s.OpenChequesAsync(new DateTime(2025, 11, 5))).OutstandingTotal.ShouldBe(0m);
        var again = await s.ConvertAsync(cheque.Id, 3, new DateTime(2025, 11, 5), s.Day.ClientId);
        await ShouldFail(again, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_CONVERSION_STATE_INVALID);

        // A later non-cash repayment nets the converted debt by the branch's FIFO like any fiado debt.
        var repayment = await _client.PostAuthAsync(
            "/transaction/fiado-payment",
            new RequestReceiveFiadoPaymentJson
            {
                Date = new DateTime(2025, 11, 5), Value = 50m, TransactionTypeId = s.Day.PixMethod.Id,
                AccountId = s.Day.TabId, ClientId = s.Day.ClientId, RecordedByOperatorId = s.Day.MemberA.Operator!.Id
            },
            s.ManagerToken);
        repayment.StatusCode.ShouldBe(HttpStatusCode.Created, await repayment.Content.ReadAsStringAsync());
        (await s.FiadoBalanceAsync(s.Day.ClientId, new DateTime(2025, 11, 5))).ShouldBe(150m);

        // The whole chain, read back: three appended events on one chain, the original row untouched.
        var events = await s.EventsAsync(cheque.Id);
        events.Select(e => (e.Sequence, e.Kind, e.Amount, e.OutstandingDelta)).ShouldBe([
            (1, SettlementEventKind.ChequeDeposited, 200m, -200m),
            (2, SettlementEventKind.ChequeReturned, 200m, 200m),
            (3, SettlementEventKind.ChequeConvertedToFiado, 200m, -200m)
        ]);
        events.Skip(1).Select(e => e.PreviousEventId).ShouldBe(events.Take(2).Select(e => (Guid?)e.Id));
        var frozenResult = await _client.FinancialResultAsync(s.ManagerToken, new DateTime(2025, 10, 2), new DateTime(2025, 10, 2));
        frozenResult.Classes.Single(c => c.FinancialClass == FinancialClass.InternalTransfer).KnownSignedTotal.ShouldBe(200m);
        var november = await _client.FinancialResultAsync(s.ManagerToken, new DateTime(2025, 11, 5));
        november.KnownOwnResult.ShouldBe(0m);
        var movements = november.Classes.Single(c => c.FinancialClass == FinancialClass.InternalTransfer);
        (movements.KnownSignedTotal, movements.KnownVolume, movements.Count).ShouldBe((0m, 400m, 2));
        november.BankPositionComplete.ShouldBeFalse();
        november.Entries.Single(e => e.SourceKind == FinancialResultSourceKind.ChequeReturn).SignedAmount.ShouldBe(-200m);
        november.Entries.ShouldNotContain(e => e.SourceId == converted.ConvertedTransactionId);
        (await factory.ReloadAsync<Transaction>(cheque.Id))!.Version.ShouldBe(original.Version);
    }

    [Fact]
    public async Task ReturnThenRepresent_ShouldStartANewAttemptWithItsOwnReceipt_AndARetryShouldNotAddAnother()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeRepresent");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 8, 20, 10, 0, 0));
        var cheque = await s.SeedChequeAsync(new DateTime(2025, 8, 1), 150m, clientId: s.Day.ClientId);

        var first = await s.DepositAsync(new DateTime(2025, 8, 5), [(cheque.Id, 0)], postedOn: new DateTime(2025, 8, 5));
        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        var firstDeposit = await first.ReadContentAsync<ResponseChequeOperationJson>();
        var depositId = firstDeposit.Receivables.Single().CurrentDepositEventId!.Value;
        firstDeposit.Receivables.Single().Events.Single().IsLatePosting.ShouldBeFalse();

        var stale = await s.ReturnAsync(cheque.Id, depositId, 0, new DateTime(2025, 8, 10), s.Day.ClientId);
        await ShouldFail(stale, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_STALE_VERSION);
        (await s.ReturnAsync(cheque.Id, depositId, 1, new DateTime(2025, 8, 10), s.Day.ClientId)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var key = $"represent-{Guid.NewGuid():N}";
        var represent = await s.RepresentAsync(cheque.Id, 2, new DateTime(2025, 8, 12), key);
        var retried = await s.RepresentAsync(cheque.Id, 2, new DateTime(2025, 8, 12), key);
        represent.StatusCode.ShouldBe(HttpStatusCode.OK, await represent.Content.ReadAsStringAsync());
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        var represented = (await represent.ReadContentAsync<ResponseChequeOperationJson>()).Receivables.Single();
        var replayed = (await retried.ReadContentAsync<ResponseChequeOperationJson>()).Receivables.Single();
        (represented.ChequeState, represented.OutstandingValue, represented.Version)
            .ShouldBe((ChequeLifecycleState.AwaitingRedeposit, 150m, 3));
        replayed.Events.Select(e => e.Id).ShouldBe(represented.Events.Select(e => e.Id));
        (await s.EventsAsync(cheque.Id)).Count.ShouldBe(3);
        var secondAttempt = await s.RepresentAsync(cheque.Id, 3, new DateTime(2025, 8, 12));
        await ShouldFail(secondAttempt, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_REPRESENT_STATE_INVALID);

        var redeposit = await s.DepositOkAsync(new DateTime(2025, 8, 15), (cheque.Id, 3));
        var final = redeposit.Receivables.Single();
        (final.Status, final.DepositAttempts, final.Version, final.CurrentDepositEventId)
            .ShouldBe((ReceivableSettlementStatus.Settled, 2, 4, (Guid?)final.Events[3].Id));
        (await s.ReceiptAsync(firstDeposit.BankReceipt!.Id)).ExpectedAmount.ShouldBe(0m);
        (redeposit.BankReceipt!.ExpectedAmount, redeposit.BankReceipt.Id == firstDeposit.BankReceipt.Id).ShouldBe((150m, false));
        (await s.EventsAsync(cheque.Id)).Select(e => e.Kind).ShouldBe([
            SettlementEventKind.ChequeDeposited, SettlementEventKind.ChequeReturned,
            SettlementEventKind.ChequeRepresented, SettlementEventKind.ChequeDeposited
        ]);
    }

    [Fact]
    public async Task CustodyThenDeposit_ShouldKeepTheClaimOpenUntilDeposit_AndBlockCancellation()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeCustody");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 7, 10, 10, 0, 0));
        var cheque = await s.SeedChequeAsync(new DateTime(2025, 7, 9), 80m);
        var rowsBefore = await s.BranchTransactionCountAsync();

        var custody = await s.CustodyAsync(new DateTime(2025, 7, 9), s.Day.SafeId, (cheque.Id, 0));
        custody.StatusCode.ShouldBe(HttpStatusCode.OK, await custody.Content.ReadAsStringAsync());
        var inSafe = (await custody.ReadContentAsync<ResponseChequeOperationJson>()).Receivables.Single();
        (inSafe.Status, inSafe.ChequeState, inSafe.OutstandingValue, inSafe.CustodyAccountId)
            .ShouldBe((ReceivableSettlementStatus.Open, ChequeLifecycleState.InSafe, 80m, (Guid?)s.Day.SafeId));
        var custodyEvent = (await s.EventsAsync(cheque.Id)).Single();
        (custodyEvent.CustodyFromAccountId, custodyEvent.CustodyToAccountId, custodyEvent.OutstandingDelta)
            .ShouldBe(((Guid?)s.Day.CashboxId, (Guid?)s.Day.SafeId, 0m));
        (await s.BranchTransactionCountAsync()).ShouldBe(rowsBefore);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
        (await s.OpenChequesAsync(new DateTime(2025, 7, 10))).OutstandingTotal.ShouldBe(80m);

        var sameSafe = await s.CustodyAsync(new DateTime(2025, 7, 10), s.Day.SafeId, (cheque.Id, 1));
        await ShouldFail(sameSafe, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_CUSTODY_SAME_LOCATION);

        var row = (await factory.ReloadAsync<Transaction>(cheque.Id))!;
        var cancel = await _client.PostAuthAsync(
            $"/transaction/{cheque.Id}/cancel", new RequestCancelTransactionJson { CancellationReason = "erro" },
            s.ManagerToken, expectedVersion: row.Version);
        await ShouldFail(cancel, HttpStatusCode.Conflict, ResourcesErrorMessages.RECEIVABLE_HAS_SETTLEMENT_EVENTS);

        var deposit = await s.DepositOkAsync(new DateTime(2025, 7, 10), (cheque.Id, 1));
        deposit.Receivables.Single().ChequeState.ShouldBe(ChequeLifecycleState.Deposited);
        deposit.BankReceipt!.Amount.ShouldBe(80m);
    }

    [Fact]
    public async Task DepositBatch_ShouldBeAllOrNothing_AndRefuseEveryInvalidInputBeforeAnyEffect()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeBatch");
        var foreign = await ChequeScenario.CreateAsync(factory, _client, "ChequeBatchForeign");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 6, 20, 10, 0, 0));
        var a = await s.SeedChequeAsync(new DateTime(2025, 6, 2), 100m);
        var b = await s.SeedChequeAsync(new DateTime(2025, 6, 3), 50m);
        var pixType = s.Day.SeededTypes["saidas.pix"];
        var notCheque = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, pixType.Id, pixType.CategoryId,
            Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id, date: new DateTime(2025, 6, 2), value: 10m);
        var draft = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, s.ChequeType.Id, s.ChequeType.CategoryId,
            Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id, date: new DateTime(2025, 6, 2), value: 30m,
            status: TransactionStatus.Draft);
        var foreignCheque = await foreign.SeedChequeAsync(new DateTime(2025, 6, 2), 70m);
        var inactiveBank = await factory.SeedAccountAsync(s.BranchId, AccountType.BankAccount, "Banco antigo", active: false);
        var day = new DateTime(2025, 6, 10);

        await ShouldFail(await s.DepositAsync(day, [(a.Id, 0), (a.Id, 0)]), HttpStatusCode.BadRequest, ResourcesErrorMessages.CHEQUE_ITEMS_DUPLICATE);
        await ShouldFail(await s.DepositAsync(day, []), HttpStatusCode.BadRequest, ResourcesErrorMessages.CHEQUE_ITEMS_REQUIRED);
        await ShouldFail(await s.DepositAsync(day, [(a.Id, 0), (b.Id, 1)]), HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_STALE_VERSION);
        await ShouldFail(await s.DepositAsync(day, [(a.Id, 0), (notCheque.Id, 0)]), HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_NOT_A_CHEQUE);
        await ShouldFail(await s.DepositAsync(day, [(a.Id, 0), (draft.Id, 0)]), HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_NOT_ACTIVE);
        await ShouldFail(await s.DepositAsync(day, [(a.Id, 0), (foreignCheque.Id, 0)]), HttpStatusCode.NotFound, ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
        foreach (var destination in new[] { s.Day.TabId, s.Day.SafeId, inactiveBank.Id, foreign.Bank.Id, Guid.NewGuid() })
            await ShouldFail(await s.DepositAsync(day, [(a.Id, 0)], bankId: destination), HttpStatusCode.NotFound, ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
        await ShouldFail(await s.DepositAsync(new DateTime(2025, 6, 21), [(a.Id, 0)]), HttpStatusCode.BadRequest, ResourcesErrorMessages.SETTLEMENT_OCCURRED_ON_IN_FUTURE);
        await ShouldFail(await s.DepositAsync(new DateTime(2025, 6, 2), [(a.Id, 0), (b.Id, 0)]), HttpStatusCode.Conflict, ResourcesErrorMessages.SETTLEMENT_OCCURRED_BEFORE_RECEIVABLE_DATE);
        await ShouldFail(await s.DepositAsync(day, [(a.Id, 0)], postedOn: new DateTime(2025, 6, 9)), HttpStatusCode.BadRequest, ResourcesErrorMessages.SETTLEMENT_POSTED_ON_BEFORE_OCCURRED_ON);
        var member = await s.DepositAsync(day, [(a.Id, 0)], token: s.Day.MemberA.Token);
        member.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await s.BranchEventCountAsync()).ShouldBe(0);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);

        var both = await s.DepositOkAsync(day, (a.Id, 0), (b.Id, 0));
        (both.BankReceipt!.Amount, both.BankReceipt.Allocations.Count, both.Receivables.Count).ShouldBe((150m, 2, 2));
        both.BankReceipt.Allocations.Select(x => x.TransactionId).Order().ShouldBe(new[] { a.Id, b.Id }.Order());
        (await s.BranchEventCountAsync()).ShouldBe(2);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
        await ShouldFail(await s.DepositAsync(day, [(a.Id, 1)]), HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_DEPOSIT_STATE_INVALID);
    }

    [Fact]
    public async Task Deposit_ShouldReplayTheSameOutcome_RefuseKeyReuse_AndLetOnlyOneConcurrentDepositWin()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeReplay");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 5, 20, 10, 0, 0));
        var a = await s.SeedChequeAsync(new DateTime(2025, 5, 2), 40m);
        var b = await s.SeedChequeAsync(new DateTime(2025, 5, 2), 60m);
        var key = $"cheque-deposit-{Guid.NewGuid():N}";

        var first = await s.DepositAsync(new DateTime(2025, 5, 3), [(a.Id, 0)], key: key);
        var replay = await s.DepositAsync(new DateTime(2025, 5, 3), [(a.Id, 0)], key: key);
        var reused = await s.DepositAsync(new DateTime(2025, 5, 4), [(a.Id, 0)], key: key);
        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await replay.ReadContentAsync<ResponseChequeOperationJson>()).BankReceipt!.Id
            .ShouldBe((await first.ReadContentAsync<ResponseChequeOperationJson>()).BankReceipt!.Id);
        await ShouldFail(reused, HttpStatusCode.Conflict, ResourcesErrorMessages.IDEMPOTENCY_KEY_PAYLOAD_CONFLICT);

        var racing = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => s.DepositAsync(new DateTime(2025, 5, 3), [(b.Id, 0)])));
        racing.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        racing.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.Conflict);
        (await s.EventsAsync(b.Id)).Count.ShouldBe(1);
        (await s.BranchReceiptCountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task FailureBeforeCommit_ShouldLeaveNoEventReceiptOrReplay_AndTheRetryShouldSucceedOnce()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeRollback");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 4, 20, 10, 0, 0));
        var cheque = await s.SeedChequeAsync(new DateTime(2025, 4, 2), 90m);
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        var key = $"cheque-rollback-{Guid.NewGuid():N}";

        HttpResponseMessage failed;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("UPDATE \"IdempotencyRequests\"", StringComparison.Ordinal),
                   _ => throw new InvalidOperationException("Injected failure after the events were written")))
        {
            failed = await s.DepositAsync(new DateTime(2025, 4, 3), [(cheque.Id, 0)], key: key);
        }

        failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await s.BranchEventCountAsync()).ShouldBe(0);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);

        var retry = await s.DepositAsync(new DateTime(2025, 4, 3), [(cheque.Id, 0)], key: key);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created, await retry.Content.ReadAsStringAsync());
        (await s.BranchEventCountAsync()).ShouldBe(1);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ReturnIdentifiedClient_ShouldStayTheDebtor_EvenIfTheRowsClientIsLaterChanged()
    {
        // Review P1 reproduction: deposit → return identifying A → edit the row to B → convert to B.
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeClientLock");
        var clientA = await factory.SeedClientAsync(s.BranchId, "Cliente A");
        var clientB = await factory.SeedClientAsync(s.BranchId, "Cliente B");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 3, 20, 10, 0, 0));
        var cheque = await s.SeedChequeAsync(new DateTime(2025, 3, 2), 100m);
        var deposit = (await s.DepositOkAsync(new DateTime(2025, 3, 3), (cheque.Id, 0))).Receivables.Single();
        (await s.ReturnAsync(cheque.Id, deposit.CurrentDepositEventId!.Value, 1, new DateTime(2025, 3, 10), clientA.Id))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var row = (await factory.ReloadAsync<Transaction>(cheque.Id))!;
        var edit = await _client.PutAuthAsync($"/transaction/{cheque.Id}",
            new RequestUpdateTransactionJson { DueDate = row.DueDate, ClientId = clientB.Id, Description = row.Description },
            s.ManagerToken, expectedVersion: row.Version);
        await ShouldFail(edit, HttpStatusCode.Conflict, ResourcesErrorMessages.RECEIVABLE_CLIENT_LOCKED_BY_EVENTS);
        var sameClient = await _client.PutAuthAsync($"/transaction/{cheque.Id}",
            new RequestUpdateTransactionJson { DueDate = row.DueDate, ClientId = null, Description = "só a descrição" },
            s.ManagerToken, expectedVersion: row.Version);
        sameClient.StatusCode.ShouldBe(HttpStatusCode.OK, await sameClient.Content.ReadAsStringAsync());

        // Even a row whose ClientId was written past the API keeps the client the immutable return identified.
        await s.QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Transactions" SET "ClientId" = {clientB.Id} WHERE "Id" = {cheque.Id}"""));
        var toB = await s.ConvertAsync(cheque.Id, 2, new DateTime(2025, 3, 20), clientB.Id);
        await ShouldFail(toB, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_CLIENT_MISMATCH);
        var toA = await s.ConvertAsync(cheque.Id, 2, new DateTime(2025, 3, 20), clientA.Id);
        toA.StatusCode.ShouldBe(HttpStatusCode.Created, await toA.Content.ReadAsStringAsync());
        var debt = (await factory.ReloadAsync<Transaction>(
            (await toA.ReadContentAsync<ResponseChequeOperationJson>()).ConvertedTransactionId!.Value))!;
        (debt.ClientId, debt.Value).ShouldBe(((Guid?)clientA.Id, 100m));
        (await s.FiadoBalanceAsync(clientB.Id, new DateTime(2025, 3, 20))).ShouldBe(0m);
        (await s.FiadoBalanceAsync(clientA.Id, new DateTime(2025, 3, 20))).ShouldBe(100m);
    }

    [Fact]
    public async Task ReclassifyingADepositedCheque_ShouldNeverStrandItsLifecycle()
    {
        // Review P2 reproduction: deposit a standalone cheque → set the type to None → reclassify the open window →
        // return the existing deposit.
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeReclassify");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 2, 20, 10, 0, 0));
        var cheque = await s.SeedChequeAsync(new DateTime(2025, 2, 2), 120m);
        var untouched = await s.SeedChequeAsync(new DateTime(2025, 2, 3), 30m);
        var deposit = (await s.DepositOkAsync(new DateTime(2025, 2, 4), (cheque.Id, 0))).Receivables.Single();

        var declassify = await _client.PutAuthAsync($"/transaction-type/{s.ChequeType.Id}", new RequestUpdateTransactionTypeJson
        {
            Name = s.ChequeType.Name,
            SettlementRule = s.ChequeType.SettlementRule,
            SettlementInstrument = SettlementInstrument.None
        }, s.ManagerToken);
        declassify.StatusCode.ShouldBe(HttpStatusCode.OK, await declassify.Content.ReadAsStringAsync());

        // Only the cheque without a lifecycle is a candidate; the deposited one keeps the instrument it was deposited as.
        var candidates = await _client.GetAuthAsync(
            $"/transaction-type/{s.ChequeType.Id}/reclassification-candidates?DateFrom=2025-02-01&DateTo=2025-02-20", s.ManagerToken);
        var listed = await candidates.ReadContentAsync<ResponseReclassificationJson>();
        listed.Rows.Select(r => r.TransactionId).ShouldBe([untouched.Id]);
        var depositedRow = (await factory.ReloadAsync<Transaction>(cheque.Id))!;
        var forced = await _client.PostAuthAsync($"/transaction-type/{s.ChequeType.Id}/reclassify",
            new RequestReclassifyTransactionTypeJson
            {
                DateFrom = new DateTime(2025, 2, 1), DateTo = new DateTime(2025, 2, 20), Reason = "Rever cheques",
                Rows = [new RequestReclassifyRowJson { TransactionId = cheque.Id, Version = depositedRow.Version }]
            }, s.ManagerToken);
        await ShouldFail(forced, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_STALE);
        var applied = await _client.PostAuthAsync($"/transaction-type/{s.ChequeType.Id}/reclassify",
            new RequestReclassifyTransactionTypeJson
            {
                DateFrom = new DateTime(2025, 2, 1), DateTo = new DateTime(2025, 2, 20), Reason = "Rever cheques",
                Rows = [new RequestReclassifyRowJson { TransactionId = untouched.Id, Version = listed.Rows[0].Version }]
            }, s.ManagerToken);
        applied.StatusCode.ShouldBe(HttpStatusCode.OK, await applied.Content.ReadAsStringAsync());

        (await factory.ReloadAsync<Transaction>(cheque.Id))!.PostedSettlementInstrument.ShouldBe(SettlementInstrument.Cheque);
        var returned = await s.ReturnAsync(cheque.Id, deposit.CurrentDepositEventId!.Value, 1, new DateTime(2025, 2, 10), s.Day.ClientId);
        returned.StatusCode.ShouldBe(HttpStatusCode.OK, await returned.Content.ReadAsStringAsync());
        var reopened = (await returned.ReadContentAsync<ResponseChequeOperationJson>()).Receivables.Single();
        (reopened.IsCheque, reopened.ChequeState, reopened.OutstandingValue).ShouldBe((true, ChequeLifecycleState.Returned, 120m));
        (await s.OpenChequesAsync(new DateTime(2025, 2, 20))).OutstandingTotal.ShouldBe(120m);
    }

    [Fact]
    public async Task ReclassificationAndDeposit_ShouldSerialize_SoTheLifecycleKeepsOneInstrumentInEitherOrder()
    {
        // Review P2 (concurrency): a deposit landing between the reclassification's candidate read and its write.
        var s = await ChequeScenario.CreateAsync(factory, _client, "ChequeReclassifyRace");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 1, 20, 10, 0, 0));
        var first = await s.SeedChequeAsync(new DateTime(2025, 1, 2), 75m);
        var second = await s.SeedChequeAsync(new DateTime(2025, 1, 3), 40m);
        var declassify = await _client.PutAuthAsync($"/transaction-type/{s.ChequeType.Id}", new RequestUpdateTransactionTypeJson
        {
            Name = s.ChequeType.Name, SettlementRule = s.ChequeType.SettlementRule, SettlementInstrument = SettlementInstrument.None
        }, s.ManagerToken);
        declassify.StatusCode.ShouldBe(HttpStatusCode.OK);
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();

        Task<HttpResponseMessage> Reclassify(Transaction row, uint version) => _client.PostAuthAsync(
            $"/transaction-type/{s.ChequeType.Id}/reclassify",
            new RequestReclassifyTransactionTypeJson
            {
                DateFrom = row.Date, DateTo = row.Date, Reason = "Rever cheques",
                Rows = [new RequestReclassifyRowJson { TransactionId = row.Id, Version = version }]
            }, s.ManagerToken);

        // Order 1: the deposit arrives while the reclassification holds its read; it waits, then sees the row reclassified.
        var firstVersion = (await factory.ReloadAsync<Transaction>(first.Id))!.Version;
        Task<HttpResponseMessage>? lateDeposit = null;
        HttpResponseMessage reclassified;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("\"SettlementEvents\"", StringComparison.Ordinal)
                              && command.CommandText.Contains("\"TransactionTypes\"", StringComparison.Ordinal),
                   async _ =>
                   {
                       lateDeposit = s.DepositAsync(new DateTime(2025, 1, 4), [(first.Id, 0)]);
                       await Task.Delay(500);
                   }))
        {
            reclassified = await Reclassify(first, firstVersion);
        }
        reclassified.StatusCode.ShouldBe(HttpStatusCode.OK, await reclassified.Content.ReadAsStringAsync());
        await ShouldFail(await lateDeposit!, HttpStatusCode.Conflict, ResourcesErrorMessages.CHEQUE_NOT_A_CHEQUE);
        (await factory.ReloadAsync<Transaction>(first.Id))!.PostedSettlementInstrument.ShouldBe(SettlementInstrument.None);
        (await s.EventsAsync(first.Id)).ShouldBeEmpty();

        // Order 2: the reclassification arrives while the deposit holds its accounts; it waits, then finds the row
        // deposited (no longer a candidate for an instrument change) and changes nothing.
        var secondVersion = (await factory.ReloadAsync<Transaction>(second.Id))!.Version;
        Task<HttpResponseMessage>? lateReclassify = null;
        HttpResponseMessage deposited;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("INSERT INTO \"BankReceipts\"", StringComparison.Ordinal),
                   async _ =>
                   {
                       lateReclassify = Reclassify(second, secondVersion);
                       await Task.Delay(500);
                   }))
        {
            deposited = await s.DepositAsync(new DateTime(2025, 1, 4), [(second.Id, 0)]);
        }
        deposited.StatusCode.ShouldBe(HttpStatusCode.Created, await deposited.Content.ReadAsStringAsync());
        await ShouldFail(await lateReclassify!, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_STALE);
        (await factory.ReloadAsync<Transaction>(second.Id))!.PostedSettlementInstrument.ShouldBe(SettlementInstrument.Cheque);
        (await s.EventsAsync(second.Id)).Single().SettlementInstrument.ShouldBe(SettlementInstrument.Cheque);

        // The returned claim stays visible: snapshot and events agree, so aging and the lifecycle never diverge.
        var depositEvent = (await s.EventsAsync(second.Id)).Single();
        (await s.ReturnAsync(second.Id, depositEvent.Id, 1, new DateTime(2025, 1, 10), s.Day.ClientId)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await s.OpenChequesAsync(new DateTime(2025, 1, 20))).OutstandingTotal.ShouldBe(40m);

        // The database refuses the divergence on its own, even if a writer skipped the coordination.
        var forced = await Should.ThrowAsync<Npgsql.PostgresException>(() => s.QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TransactionClassificationRevisions" ("Id", "BranchId", "TransactionId", "BatchId", "Revision",
                "SettlementInstrument", "MonthlyFinancialRole", "FinancialClass", "Reason", "CreatedByUserId", "CreatedAt", "Active")
            VALUES ({Guid.NewGuid()}, {s.BranchId}, {second.Id}, {Guid.NewGuid()}, 2, 0, 0, 0, 'forçado', {s.Day.Manager.User.Id}, now(), true);
            UPDATE "Transactions" SET "PostedSettlementInstrument" = 0, "ClassificationProvenance" = 3, "ClassificationRevision" = 2
            WHERE "Id" = {second.Id};
            """)));
        forced.ConstraintName.ShouldBe("CK_Transactions_ClassificationFrozen");
    }

    private static async Task ShouldFail(HttpResponseMessage response, HttpStatusCode status, string key)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(key);
    }
}
