using System.Net;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M9.6 Phase 6 golden journeys (§6.19 O05–O07, O19; E04/E05) on a branch seeded by the real bootstrap: every new
/// counter fiado sale pairs its cashbox Out on the custodian's session, a dedicated debt origin writes no cashbox leg,
/// and repayment requires an explicit method whose kind alone decides the drawer effect.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class FiadoBoundariesGoldenTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task E04BillOrGameOnFiado_ShouldReconcileWithTheReport_AndOweTheClient()
    {
        // O=1,000; bill/game 240 on fiado with R=240 (report field); C=1,000 → I=240 U=240 → 0; D=240.
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "GoldenE04Bill");
        var sessionId = await s.StartAsync();

        var sale = await Created("/transaction", s.FiadoSale(240m), s.MemberA.Token);
        await s.SaveOkAsync(sessionId, cash: 1_000m, fields: FinancialDayScenario.Fields(report: 240m));

        var rows = await s.RowsAsync();
        var tabRow = rows.Single(r => r.Id == sale.Id);
        var leg = rows.Single(r => r.Id == sale.GeneratedTransactionId);
        (tabRow.AccountId, tabRow.SessionId, tabRow.Direction).ShouldBe((s.TabId, (Guid?)null, Direction.Out));
        (leg.AccountId, leg.SessionId, leg.Direction, leg.Value, leg.GenerationKind)
            .ShouldBe((s.CashboxId, (Guid?)sessionId, Direction.Out, 240m, (TransactionGenerationKind?)TransactionGenerationKind.FiadoSale));
        leg.TransactionTypeId.ShouldBe(s.SystemTypes[TransactionTypeSystemRole.FiadoSaleCounterpart].Id);
        var attempt = (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt;
        (attempt.TotalIn, attempt.TotalOut, attempt.Difference).ShouldBe((240m, 240m, 0m));
        (await s.OutstandingAsync()).ShouldBe(240m);
    }

    [Fact]
    public async Task E04CountedStockOnFiado_ShouldReconcileWithoutAReport()
    {
        // O=1,500 (cash 1,000 + stock 500); stock 80 on fiado, R=0 → C=1,420 → I=0 U=80 → 0; D=80.
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "GoldenE04Stock", openingStock: 500m);
        var sessionId = await s.StartAsync();

        await Created("/transaction", s.FiadoSale(80m), s.MemberA.Token);
        await s.SaveOkAsync(sessionId, cash: 1_000m, stock: 420m);

        var attempt = (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt;
        (attempt.TotalOpening, attempt.TotalIn, attempt.TotalOut, attempt.Difference).ShouldBe((1_500m, 0m, 80m, 0m));
        (await s.OutstandingAsync()).ShouldBe(80m);
    }

    [Fact]
    public async Task E05OpeningDebtWithCashAndPixRepayments_ShouldMoveOnlyWhatPhysicallyMoved()
    {
        // Opening debt 240 (dedicated origin: no cashbox leg); repay 90 cash → C=1,090 → I=90 → 0; D=150. Then PIX 160
        // leaves the drawer unchanged and the client with credit 10 (FIFO branch-wide, no open sale).
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "GoldenE05");
        var sessionId = await s.StartAsync();
        await s.SeedOpeningDebtAsync(240m);
        var beforeDebt = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        beforeDebt.Count!.LedgerRevision.ShouldBe(0);

        var cash = await Created("/transaction/fiado-payment", s.Repayment(s.CashMethod, 90m), s.MemberA.Token);
        await s.SaveOkAsync(sessionId, cash: 1_090m);
        var attempt = (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt;
        (attempt.TotalIn, attempt.TotalOut, attempt.Difference).ShouldBe((90m, 0m, 0m));
        (await s.OutstandingAsync()).ShouldBe(150m);
        (await s.RowsAsync()).Single(r => r.Id == cash.GeneratedTransactionId).SessionId.ShouldBe(sessionId);

        var pix = await Created("/transaction/fiado-payment", s.Repayment(s.PixMethod, 160m), s.MemberA.Token);
        pix.GeneratedTransactionId.ShouldBeNull();
        pix.SessionId.ShouldBeNull();
        var afterPix = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        afterPix.Count!.CandidateState.ShouldBe(SessionCandidateState.Current);
        (await s.OutstandingAsync()).ShouldBe(-10m);
        (await s.RowsAsync()).Count(r => r.AccountId == s.CashboxId).ShouldBe(1);
    }

    [Fact]
    public async Task Repayment_ShouldRequireAnExplicitValidMethod_AndWriteNothingOtherwise()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "GoldenMethod");
        var sessionId = await s.StartAsync();
        await s.SeedOpeningDebtAsync(50m);
        var before = await _client.GetSessionAsync(sessionId, s.MemberA.Token);

        var missing = s.Repayment(s.CashMethod, 10m);
        var noMethod = await _client.PostAuthAsync(
            "/transaction/fiado-payment",
            new RequestReceiveFiadoPaymentJson { Date = missing.Date, Value = 10m, AccountId = s.TabId, ClientId = s.ClientId },
            s.MemberA.Token);
        await ShouldFail(noMethod, HttpStatusCode.BadRequest, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_METHOD_INVALID);
        var notAMethod = await _client.PostAuthAsync("/transaction/fiado-payment", s.Repayment(s.FiadoSaleType, 10m), s.MemberA.Token);
        await ShouldFail(notAMethod, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_METHOD_INVALID);
        var legacyCliente = s.SeededTypes["entradas.cliente"];
        var unconfigured = await _client.PostAuthAsync("/transaction/fiado-payment", s.Repayment(legacyCliente, 10m), s.MemberA.Token);
        await ShouldFail(unconfigured, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_METHOD_INVALID);

        (await s.RowsAsync()).Count.ShouldBe(1);
        (await s.OutstandingAsync()).ShouldBe(50m);
        var after = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        (after.Count!.LedgerRevision, after.Count.ConfirmationToken).ShouldBe((before.Count!.LedgerRevision, before.Count.ConfirmationToken));
    }

    [Fact]
    public async Task UnpairedTab_ShouldRefuseTheSale_AndPairingShouldGrantNothing()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "GoldenUnpaired");
        await s.StartAsync();

        var unpairedCashbox = await _client.PostAuthAsync(
            "/account/terminal", new RequestCreateTerminalAccountJson { Name = "Caixa sem fiado", CreateTabAccount = false }, s.Manager.Token);
        unpairedCashbox.StatusCode.ShouldBe(HttpStatusCode.Created, await unpairedCashbox.Content.ReadAsStringAsync());
        (await unpairedCashbox.ReadContentAsync<ResponseCreateAccountJson>()).TabAccountId.ShouldBeNull();
        var looseTab = await factory.SeedAccountAsync(s.Branch.Id, AccountType.Tab, "Fiado avulso");

        var sale = s.FiadoSale(30m);
        var refused = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJson
            {
                Date = sale.Date, Value = sale.Value, TransactionTypeId = sale.TransactionTypeId, AccountId = looseTab.Id,
                ClientId = s.ClientId, RecordedByOperatorId = s.MemberA.Operator!.Id
            },
            s.Manager.Token);
        await ShouldFail(refused, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED);

        // Member B holds the cashbox and Tab grants, but A has custody: the generated leg needs the active custodian.
        var notCustodian = await _client.PostAuthAsync("/transaction", s.FiadoSale(30m), s.MemberB.Token);
        await ShouldFail(notCustodian, HttpStatusCode.Forbidden, ResourcesErrorMessages.SESSION_NOT_CUSTODIAN);

        // The paired Tab is no permission by itself: without the explicit Tab grant the sale is refused.
        await factory.ExecuteSqlAsync($"""
            UPDATE "OperatorAccounts" SET "Active" = false WHERE "OperatorId" = '{s.MemberA.Operator!.Id}' AND "AccountId" = '{s.TabId}'
            """);
        var withoutTabGrant = await _client.PostAuthAsync("/transaction", s.FiadoSale(30m), s.MemberA.Token);
        await ShouldFail(withoutTabGrant, HttpStatusCode.Forbidden, ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        (await s.RowsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ManagerSale_ShouldNameTheExactSession_AndInlineOpeningShouldStartItAtomically()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "GoldenInline");

        // No session yet: the sale carries the opening acknowledgment, starting custody with the pair in one commit.
        var acknowledgment = await _client.BuildOpeningAcknowledgmentAsync(s.CashboxId, s.MemberA.Token);
        var inline = s.FiadoSale(25m);
        var first = await Created(
            "/transaction",
            new RequestCreateTransactionJson
            {
                Date = inline.Date, Value = 25m, TransactionTypeId = inline.TransactionTypeId, AccountId = s.TabId,
                ClientId = s.ClientId, OpeningAcknowledgment = acknowledgment
            },
            s.MemberA.Token);
        first.StartedSessionId.ShouldNotBeNull();
        var sessionId = first.StartedSessionId!.Value;
        (await s.RowsAsync()).Single(r => r.Id == first.GeneratedTransactionId).SessionId.ShouldBe(sessionId);

        // A manager naming the exact Draft session with a reason records on it; no session is ever started silently.
        var named = await Created(
            "/transaction",
            new RequestCreateTransactionJson
            {
                Date = s.Today, Value = 15m, TransactionTypeId = s.FiadoSaleType.Id, AccountId = s.TabId, ClientId = s.ClientId,
                RecordedByOperatorId = s.MemberA.Operator!.Id, SessionId = sessionId, HistoricalReason = "Venda lançada pelo gestor"
            },
            s.Manager.Token);
        (await s.RowsAsync()).Single(r => r.Id == named.GeneratedTransactionId).SessionId.ShouldBe(sessionId);
        (await s.QueryAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(db.DailyCloses, d => d.AccountId == s.CashboxId))).ShouldBe(1);

        await s.SaveOkAsync(sessionId, cash: 1_000m, fields: FinancialDayScenario.Fields(report: 40m));
        (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt.Difference.ShouldBe(0m);
        (await s.OutstandingAsync()).ShouldBe(40m);
    }

    [Fact]
    public async Task FailedPairLeg_ShouldRollBackTheInlineSessionTheSaleAndItsDebt()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "GoldenRollback");
        var acknowledgment = await _client.BuildOpeningAcknowledgmentAsync(s.CashboxId, s.MemberA.Token);
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();

        HttpResponseMessage response;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("INSERT INTO \"Transactions\"", StringComparison.Ordinal),
                   _ => throw new InvalidOperationException("Injected pair failure")))
        {
            response = await _client.PostAuthAsync(
                "/transaction",
                new RequestCreateTransactionJson
                {
                    Date = s.Today, Value = 25m, TransactionTypeId = s.FiadoSaleType.Id, AccountId = s.TabId,
                    ClientId = s.ClientId, OpeningAcknowledgment = acknowledgment
                },
                s.MemberA.Token);
        }

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await s.RowsAsync()).ShouldBeEmpty();
        (await s.QueryAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .CountAsync(db.DailyCloses, d => d.AccountId == s.CashboxId))).ShouldBe(0);
        (await s.OutstandingAsync()).ShouldBe(0m);
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
