using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>
/// M9.6 Phase 10 demonstration through the API on a bootstrap-seeded branch: a shared cashbox (Member A → handover with
/// matching or different opening → Member B) beside a separate cashbox on the same physical terminal, inline opening by the
/// first fiado sale, every fiado boundary, signed report and fees, a cofre deposit, blind count with a ledger change
/// between Conferir and Submit (recount), and duty-checked day approval. Every number below is computed independently
/// and every persisted amount is checked exactly once.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class M9_6IntegratedDayDemonstrationTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedAndSeparateCustody_ShouldCloseTheWholeDay_WithEveryAmountExplainedOnce(bool differentOpening)
    {
        // Opening count of the shared cashbox: cash 1,000 + stock 500.
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "DemoDay", openingCash: 1_000m, openingStock: 500m);
        var terminalId = await s.QueryAsync(db => db.Accounts.Where(a => a.Id == s.CashboxId).Select(a => a.TerminalId!.Value).SingleAsync());
        await s.SeedOpeningDebtAsync(240m); // dedicated origin: no cashbox leg

        // ── Shared cashbox, Member A: the first operation (bill/game 240 on fiado) starts custody inline.
        var acknowledgment = await _client.BuildOpeningAcknowledgmentAsync(s.CashboxId, s.MemberA.Token);
        var inlineSale = await Created("/transaction", WithOpening(s.FiadoSale(240m), acknowledgment), s.MemberA.Token);
        var sessionA = inlineSale.StartedSessionId!.Value;
        await Created("/transaction", s.FiadoSale(80m), s.MemberA.Token);                              // stock 80 on fiado
        await Created("/transaction/fiado-payment", s.Repayment(s.CashMethod, 90m), s.MemberA.Token);  // cash +90
        var pix = await Created("/transaction/fiado-payment", s.Repayment(s.PixMethod, 50m), s.MemberA.Token);
        pix.GeneratedTransactionId.ShouldBeNull();
        var deposit = await Created("/transaction/safe-deposit", s.Deposit(250m), s.MemberA.Token);     // cash −250 → cofre

        // ── Separate cashbox on the same physical terminal: A operates it in parallel (explicit zero opening).
        var cashbox2 = await CreateCashboxOnTerminal(s, terminalId);
        var zero = await _client.BuildOpeningAcknowledgmentAsync(cashbox2.Id, s.MemberA.Token);
        zero.ExplicitZero.ShouldBeTrue();
        var parallelSale = await Created(
            "/transaction",
            WithOpening(new RequestCreateTransactionJson
            {
                Date = s.Today, Value = 30m, TransactionTypeId = s.FiadoSaleType.Id, AccountId = cashbox2.TabAccountId!.Value, ClientId = s.ClientId
            }, zero),
            s.MemberA.Token);
        var sessionC = parallelSale.StartedSessionId!.Value;
        sessionC.ShouldNotBe(sessionA);

        // A counts cash 1,000 + 35 (bolão) + 90 − 250 = 875 and stock 420; R=+240 explains the bill on fiado.
        await s.SaveOkAsync(sessionA, cash: 875m, stock: 420m, fields: FinancialDayScenario.Fields(report: 240m, bolao: 35m));
        var first = (await _client.ConferAsync(sessionA, s.MemberA.Token)).Attempt;
        (first.TotalOpening, first.TotalIn, first.TotalOut, first.Difference).ShouldBe((1_500m, 365m, 570m, 0m));

        // Ledger change between Conferir and Submit: Tarifa Especial 15 was also received.
        var changed = await s.SaveOkAsync(sessionA, cash: 875m, stock: 420m, fields: FinancialDayScenario.Fields(special: 15m));
        changed.Count!.CandidateState.ShouldBe(SessionCandidateState.Unconfirmed);
        var refused = await _client.PostSubmitAsync(changed, s.MemberA.Token, first.Id);
        await ShouldFail(refused, HttpStatusCode.Conflict, ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT);
        var recount = await _client.PostAuthAsync(
            $"/session/{sessionA}/recount",
            new RequestBeginSessionRecountJson { PreviousAttemptId = first.Id },
            s.MemberA.Token,
            expectedVersion: changed.Version);
        recount.StatusCode.ShouldBe(HttpStatusCode.OK, await recount.Content.ReadAsStringAsync());
        await s.SaveOkAsync(sessionA, cash: 890m, stock: 420m);
        var second = (await _client.ConferAsync(sessionA, s.MemberA.Token)).Attempt;
        (second.Kind, second.TotalIn, second.TotalOut, second.Difference).ShouldBe((SessionCountAttemptKind.Recount, 380m, 570m, 0m));
        await _client.SubmitAsync(sessionA, s.MemberA.Token);
        (await _client.GetSessionAsync(sessionA, s.MemberA.Token)).Count!.SubmittedAttemptId.ShouldBe(second.Id);

        // Separate cashbox: R=+30 explains its fiado sale; count 0 → 0. Submitted and finished on its own.
        await s.SaveOkAsync(sessionC, cash: 0m, stock: 0m, fields: FinancialDayScenario.Fields(report: 30m));
        (await _client.ConferAsync(sessionC, s.MemberA.Token)).Attempt.Difference.ShouldBe(0m);
        await _client.SubmitAsync(sessionC, s.MemberA.Token);
        await _client.FinishAsync(sessionC, s.MemberA.Token);

        // Handover A to B: matching cash 890, or 880 with a separate -10 discrepancy; A stays unchanged.
        var offer = await _client.OfferAsync(sessionA, s.MemberB.Operator!.Id, s.MemberA.Token);
        var accept = await _client.PostAuthAsync(
            $"/session/handover/{offer.Id}/accept",
            new RequestAcceptSessionHandoverJson
            {
                Items =
                [
                    new RequestSessionOpeningItemJson { ProductId = s.Cash.Id, Value = differentOpening ? 880m : 890m },
                    new RequestSessionOpeningItemJson { ProductId = s.Stock.Id, Value = 420m }
                ]
            },
            s.MemberB.Token,
            expectedVersion: offer.Version);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());
        var accepted = await accept.ReadContentAsync<ResponseAcceptSessionHandoverJson>();
        var sessionB = accepted.Incoming.Session.Id;
        if (differentOpening)
        {
            var discrepancy = accepted.Incoming.OpeningDiscrepancy.ShouldNotBeNull();
            (discrepancy.CurrentTotalDelta, discrepancy.Attribution).ShouldBe((-10m, OpeningDiscrepancyAttribution.Unattributed));
        }
        else
            accepted.Incoming.OpeningDiscrepancy.ShouldBeNull();

        // B's count is recorded, confirmed and submitted by the Manager for B (so the Manager is recorder and
        // submitter, never its approver): report -60 gives cash 820 (different) or 830 (matching), stock 420.
        await s.SaveOkAsync(sessionB, cash: differentOpening ? 820m : 830m, stock: 420m, fields: FinancialDayScenario.Fields(report: -60m), actor: s.Manager);
        var attemptB = (await _client.ConferAsync(sessionB, s.Manager.Token)).Attempt;
        (attemptB.TotalOpening, attemptB.TotalIn, attemptB.TotalOut, attemptB.Difference).ShouldBe((differentOpening ? 1_300m : 1_310m, 0m, 60m, 0m));
        await _client.SubmitAsync(sessionB, s.Manager.Token);
        await _client.FinishAsync(sessionB, s.MemberB.Token);

        // ── Approval of the shared cashbox's day: the recorder Manager is refused for B (all or nothing); the
        // independent Approver approves both; a Member cannot approve at all.
        var readA = await _client.GetSessionAsync(sessionA, s.Manager.Token);
        var readB = await _client.GetSessionAsync(sessionB, s.Manager.Token);
        var batch = new RequestApproveSessionBatchJson
        {
            Sessions =
            [
                new RequestApproveSessionBatchItemJson { SessionId = readA.Id, Version = readA.Version },
                new RequestApproveSessionBatchItemJson { SessionId = readB.Id, Version = readB.Version }
            ]
        };
        var byRecorder = await _client.PostAuthAsync("/session/approve-day", batch, s.Manager.Token);
        byRecorder.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await byRecorder.Content.ReadAsStringAsync());
        (await byRecorder.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([
            string.Format(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_ITEM_FAILED, sessionB, ResourcesErrorMessages.SESSION_APPROVER_IS_RECORDER)
        ]);
        (await _client.GetSessionAsync(sessionA, s.Manager.Token)).Status.ShouldBe(DailyCloseStatus.Submitted);
        (await _client.PostAuthAsync("/session/approve-day", batch, s.MemberB.Token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var approved = await _client.PostAuthAsync("/session/approve-day", batch, s.Approver.Token);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());

        // ── Persisted outcomes.
        var rows = await s.RowsAsync();
        var active = rows.Where(r => r.Status == TransactionStatus.Active).ToList();
        decimal Sum(Guid? sessionId, Direction direction) =>
            active.Where(r => r.SessionId == sessionId && r.Direction == direction).Sum(r => r.Value);
        (Sum(sessionA, Direction.In), Sum(sessionA, Direction.Out)).ShouldBe((380m, 570m));
        (Sum(sessionB, Direction.In), Sum(sessionB, Direction.Out)).ShouldBe((0m, 60m));
        (Sum(sessionC, Direction.In), Sum(sessionC, Direction.Out)).ShouldBe((30m, 30m));
        // Fiado legs: three sales (240, 80, 30) and one cash repayment (90), each exactly once and on its own cashbox.
        active.Where(r => r.GenerationKind == TransactionGenerationKind.FiadoSale).Select(r => (r.SessionId, r.Value)).OrderBy(x => x.Value)
            .ShouldBe([((Guid?)sessionC, 30m), (sessionA, 80m), (sessionA, 240m)]);
        active.Single(r => r.GenerationKind == TransactionGenerationKind.FiadoCashRepayment).SessionId.ShouldBe(sessionA);
        // Close fields: one live effect per field and session; the cofre holds 250 with no session.
        active.Where(r => r.GenerationKind is TransactionGenerationKind.SessionTerminalReport or TransactionGenerationKind.SessionBolaoFee
                or TransactionGenerationKind.SessionSpecialFee)
            .Select(r => (r.SessionId, r.GenerationKind, r.Direction, r.Value)).OrderBy(x => x.Value)
            .ShouldBe([
                ((Guid?)sessionA, (TransactionGenerationKind?)TransactionGenerationKind.SessionSpecialFee, Direction.In, 15m),
                (sessionC, TransactionGenerationKind.SessionTerminalReport, Direction.In, 30m),
                (sessionA, TransactionGenerationKind.SessionBolaoFee, Direction.In, 35m),
                (sessionB, TransactionGenerationKind.SessionTerminalReport, Direction.Out, 60m),
                (sessionA, TransactionGenerationKind.SessionTerminalReport, Direction.In, 240m)
            ]);
        var safeRow = active.Single(r => r.AccountId == s.SafeId);
        (safeRow.Value, safeRow.SessionId, safeRow.GeneratedFromTransactionId).ShouldBe((250m, (Guid?)null, (Guid?)deposit.Id));
        // Client debt: opening 240 + 240 + 80 + 30 − 90 (cash) − 50 (PIX) = 450.
        (await s.OutstandingAsync()).ShouldBe(450m);

        // Official variances are zero; a different handover opening stays separate and unattributed.
        foreach (var sessionId in new[] { sessionA, sessionB, sessionC })
        {
            var read = await _client.GetSessionAsync(sessionId, s.Manager.Token);
            read.Items.Single(i => i.ProductId == s.CashVariance.Id).Value.ShouldBe(0m);
        }
        (await _client.GetSessionAsync(sessionA, s.Manager.Token)).Status.ShouldBe(DailyCloseStatus.Approved);
        (await _client.GetSessionAsync(sessionB, s.Manager.Token)).Status.ShouldBe(DailyCloseStatus.Approved);
        var queue = await (await _client.GetAuthAsync($"/report/manager-queue?date={s.Today:yyyy-MM-dd}", s.Manager.Token))
            .ReadContentAsync<ResponseManagerQueueJson>();
        if (differentOpening)
            queue.Custody.OpeningDiscrepanciesAwaitingAttribution.ShouldContain(d => d.SessionId == sessionB && d.CurrentTotalDelta == -10m);
        else
            queue.Custody.OpeningDiscrepanciesAwaitingAttribution.ShouldNotContain(d => d.SessionId == sessionB);
    }

    private static RequestCreateTransactionJson WithOpening(RequestCreateTransactionJson request, RequestSessionOpeningAcknowledgmentJson ack) => new()
    {
        Date = request.Date,
        Value = request.Value,
        TransactionTypeId = request.TransactionTypeId,
        AccountId = request.AccountId,
        ClientId = request.ClientId,
        OpeningAcknowledgment = ack
    };

    private async Task<ResponseCreateAccountJson> CreateCashboxOnTerminal(FinancialDayScenario s, Guid terminalId)
    {
        var response = await _client.PostAuthAsync(
            "/account/terminal",
            new RequestCreateTerminalAccountJson { Name = "Caixa 2 — mesmo terminal", TerminalId = terminalId },
            s.Manager.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var cashbox = await response.ReadContentAsync<ResponseCreateAccountJson>();
        cashbox.TerminalId.ShouldBe(terminalId);
        await factory.SeedOperatorAccountAsync(s.MemberA.Operator!.Id, cashbox.Id);
        await factory.SeedOperatorAccountAsync(s.MemberA.Operator!.Id, cashbox.TabAccountId!.Value);
        return cashbox;
    }

    private async Task<ResponseCreateTransactionJson> Created<T>(string url, T request, string token)
    {
        var response = await _client.PostAuthAsync(url, request, token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseCreateTransactionJson>();
    }

    private static async Task ShouldFail(HttpResponseMessage response, HttpStatusCode status, string key)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(key);
    }
}
