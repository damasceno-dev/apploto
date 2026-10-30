using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>Owner decisions F1 (finish a stale Draft custody) and F2 (declare and rearrange past sessions).</summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerPastSessionTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CustodyException_ShouldLetAManagerFinishADraftSession_WhichStaysASubmitAndLockBlocker()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "F1Finish");
        var started = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        var operation = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 40m, SessionFlow.LocalToday());
        operation.StatusCode.ShouldBe(HttpStatusCode.Created);

        var withoutReason = await _client.PostAuthAsync(
            $"/session/{started.Session.Id}/custody-exception",
            new RequestResolveCustodyExceptionJson { Action = CustodyExceptionAction.Finish },
            scenario.Manager.Token,
            expectedVersion: started.Session.Version);
        withoutReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var current = await _client.GetSessionAsync(started.Session.Id, scenario.Manager.Token);
        var response = await _client.PostAuthAsync(
            $"/session/{started.Session.Id}/custody-exception",
            new RequestResolveCustodyExceptionJson { Action = CustodyExceptionAction.Finish, Reason = "Operador saiu sem fechar" },
            scenario.Manager.Token,
            expectedVersion: current.Version);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var resolved = await response.ReadContentAsync<ResponseResolveCustodyExceptionJson>();

        resolved.OutgoingSession.Custody.CustodyState.ShouldBe(SessionCustodyState.Finished);
        resolved.OutgoingSession.Status.ShouldBe(DailyCloseStatus.Draft);
        resolved.OutgoingSession.Custody.EndedAt.ShouldNotBeNull();

        // Nothing is fabricated: the next start proposes from the latest counted source (none here) and the uncounted
        // Draft with activity blocks the successor's submit.
        var next = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberB.Token);
        await _client.CountAsync(next.Session.Id, scenario.MemberB.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 0m });
        var submit = await _client.PostAuthAsync($"/session/{next.Session.Id}/submit", scenario.MemberB.Token);
        submit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeclareAndRearrange_ShouldKeepSequenceInIntervalOrder_AndMoveRowsWithTheSession()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "F2Declare");
        var today = SessionFlow.LocalToday();
        var twoDaysAgo = today.AddDays(-2);
        var current = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        var declare = await _client.PostAuthAsync(
            "/session/declare",
            new RequestDeclarePastSessionJson
            {
                AccountId = scenario.Cashbox.Id,
                CustodianOperatorId = scenario.MemberB.Operator!.Id,
                StartedAt = SaoPaulo(twoDaysAgo, 8),
                EndedAt = SaoPaulo(twoDaysAgo, 12),
                Reason = "Turno esquecido"
            },
            scenario.Manager.Token);
        declare.StatusCode.ShouldBe(HttpStatusCode.Created, await declare.Content.ReadAsStringAsync());
        var declared = (await declare.ReadContentAsync<ResponseSessionChainChangeJson>()).Session;

        declared.Custody.Origin.ShouldBe(SessionOrigin.ManagerDeclared);
        declared.Custody.CustodyState.ShouldBe(SessionCustodyState.Finished);
        declared.Custody.OpeningAcknowledged.ShouldBeFalse();
        declared.Custody.Sequence.ShouldBe(1);
        declared.Date.ShouldBe(twoDaysAgo);
        (await _client.GetSessionAsync(current.Session.Id, scenario.MemberA.Token)).Custody.Sequence.ShouldBe(2);

        // An overlapping declaration is refused by the database guard, nothing written.
        var overlap = await _client.PostAuthAsync(
            "/session/declare",
            new RequestDeclarePastSessionJson
            {
                AccountId = scenario.Cashbox.Id,
                CustodianOperatorId = scenario.MemberB.Operator!.Id,
                StartedAt = SaoPaulo(twoDaysAgo, 11),
                EndedAt = SaoPaulo(twoDaysAgo, 13),
                Reason = "Duplicado"
            },
            scenario.Manager.Token);
        overlap.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await overlap.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldHaveSingleItem()
            .ShouldBe(ResourcesErrorMessages.SESSION_DECLARE_OVERLAP);

        // A historical manager entry names the declared session; then the session moves one day later with its row.
        var entry = await _client.PostTransactionAsync(scenario, scenario.Manager.Token, 70m, twoDaysAgo, sessionId: declared.Id, reason: "Venda do turno esquecido",
            recordedByOperatorId: scenario.MemberB.Operator!.Id);
        entry.StatusCode.ShouldBe(HttpStatusCode.Created, await entry.Content.ReadAsStringAsync());
        var row = await entry.ReadContentAsync<ResponseCreateTransactionJson>();

        var reloaded = await _client.GetSessionAsync(declared.Id, scenario.Manager.Token);
        var rearrange = await _client.PostAuthAsync(
            $"/session/{declared.Id}/rearrange",
            new RequestRearrangePastSessionJson
            {
                StartedAt = SaoPaulo(today.AddDays(-1), 8),
                EndedAt = SaoPaulo(today.AddDays(-1), 12),
                Reason = "Data registrada errada"
            },
            scenario.Manager.Token,
            expectedVersion: reloaded.Version);
        rearrange.StatusCode.ShouldBe(HttpStatusCode.OK, await rearrange.Content.ReadAsStringAsync());
        var change = await rearrange.ReadContentAsync<ResponseSessionChainChangeJson>();

        change.Session.Date.ShouldBe(today.AddDays(-1));
        change.MovedTransactionIds.ShouldBe([row.Id]);
        var movedRow = (await factory.ReloadAsync<Transaction>(row.Id)).ShouldNotBeNull();
        movedRow.Date.ShouldBe(today.AddDays(-1));
        movedRow.DueDate.ShouldBe(row.DueDate);
        movedRow.SessionId.ShouldBe(declared.Id);
    }

    [Fact]
    public async Task Rearrange_ShouldNeverMoveActiveCustody()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "F2Active");
        var current = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        var response = await _client.PostAuthAsync(
            $"/session/{current.Session.Id}/rearrange",
            new RequestRearrangePastSessionJson
            {
                StartedAt = SaoPaulo(SessionFlow.LocalToday().AddDays(-3), 8),
                EndedAt = SaoPaulo(SessionFlow.LocalToday().AddDays(-3), 9),
                Reason = "Tentativa"
            },
            scenario.Manager.Token,
            expectedVersion: current.Session.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldHaveSingleItem()
            .ShouldBe(ResourcesErrorMessages.SESSION_REARRANGE_NOT_ALLOWED);
    }

    [Fact]
    public async Task Declare_ShouldRefuseAWindowSpanningTwoBusinessDays()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "F2Window");
        var day = SessionFlow.LocalToday().AddDays(-3);

        var response = await _client.PostAuthAsync(
            "/session/declare",
            new RequestDeclarePastSessionJson
            {
                AccountId = scenario.Cashbox.Id,
                CustodianOperatorId = scenario.MemberA.Operator!.Id,
                StartedAt = SaoPaulo(day, 20),
                EndedAt = SaoPaulo(day.AddDays(1), 2),
                Reason = "Virada"
            },
            scenario.Manager.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static DateTime SaoPaulo(DateTime businessDate, int hour)
    {
        return DateTime.SpecifyKind(businessDate.Date.AddHours(hour + 3), DateTimeKind.Utc);
    }
}
