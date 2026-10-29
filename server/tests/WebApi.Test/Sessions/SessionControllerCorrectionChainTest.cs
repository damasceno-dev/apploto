using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>
/// Opening-chain and follow-up rules across inserted sessions, inline handover acceptance and replay authority.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerCorrectionChainTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FirstOperation_ShouldAcceptThePendingHandoverAtomically()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "InlineAccept");
        var outgoing = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await _client.CountAsync(outgoing.Session.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 200m });
        await _client.SubmitAsync(outgoing.Session.Id, scenario.MemberA.Token);
        var offer = await _client.OfferAsync(outgoing.Session.Id, scenario.MemberB.Operator!.Id, scenario.MemberA.Token);

        var response = await _client.PostTransactionAsync(
            scenario,
            scenario.MemberB.Token,
            25m,
            SessionFlow.LocalToday(),
            new RequestSessionOpeningAcknowledgmentJson
            {
                HandoverId = offer.Id,
                HandoverVersion = offer.Version,
                Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 200m }]
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.ReadContentAsync<ResponseCreateTransactionJson>();
        created.StartedSessionId.ShouldNotBeNull();
        created.SessionId.ShouldBe(created.StartedSessionId);
        created.OpeningDiscrepancyId.ShouldBeNull();
        var incoming = await _client.GetSessionAsync(created.StartedSessionId!.Value, scenario.MemberB.Token);
        incoming.Custody.CustodianOperatorId.ShouldBe(scenario.MemberB.Operator!.Id);
        incoming.Custody.Origin.ShouldBe(SessionOrigin.HandoverAccepted);
        (await _client.GetSessionAsync(outgoing.Session.Id, scenario.MemberA.Token)).Custody.CustodyState
            .ShouldBe(SessionCustodyState.HandedOver);
    }

    [Fact]
    public async Task InsertedPastSession_ShouldReviseTheAcknowledgedSuccessor_AndKeepARevisedDiscrepancyInTheQueue()
    {
        // A closes at 100; C acknowledged 100 naming A; B is declared between them. B's first count now precedes C.
        var scenario = await SessionScenario.CreateAsync(factory, "InsertedChain");
        await factory.EnsureCashVarianceProductAsync(scenario.Branch.Id);
        var day = SessionFlow.LocalToday().AddDays(-3);
        var a = await factory.SeedDailyCloseAsync(scenario.Branch.Id, scenario.Cashbox.Id, day, DailyCloseStatus.Approved,
            custodyState: SessionCustodyState.Finished);
        await factory.SeedDailyCloseItemAsync(a.Id, scenario.Cash.Id, 100m);
        var c = await factory.SeedDailyCloseAsync(scenario.Branch.Id, scenario.Cashbox.Id, day, DailyCloseStatus.Draft,
            itemsRecorded: false, custodyState: SessionCustodyState.Finished);
        await AcknowledgeAsync(c, a.Id, scenario.Cash.Id, 100m);

        // Seeded intervals: A [12:00, 12:01) and C [12:02, 12:03) São Paulo; B fits between them.
        var declare = await _client.PostAuthAsync(
            "/session/declare",
            new RequestDeclarePastSessionJson
            {
                AccountId = scenario.Cashbox.Id,
                CustodianOperatorId = scenario.MemberB.Operator!.Id,
                StartedAt = SaoPaulo(day, 12, 1, 10),
                EndedAt = SaoPaulo(day, 12, 1, 50),
                Reason = "Turno esquecido entre dois"
            },
            scenario.Manager.Token);
        declare.StatusCode.ShouldBe(HttpStatusCode.Created, await declare.Content.ReadAsStringAsync());
        var b = (await declare.ReadContentAsync<ResponseSessionChainChangeJson>()).Session;
        b.Custody.Sequence.ShouldBe(2);

        await _client.CountAsync(b.Id, scenario.Manager.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 90m });

        var review = await _client.GetAuthAsync($"/session/{c.Id}/review", scenario.Manager.Token);
        var cReview = await review.ReadContentAsync<ResponseDailyCloseReviewJson>();
        cReview.Status.ShouldBe(DailyCloseStatus.Draft);
        var discrepancy = cReview.OpeningDiscrepancy.ShouldNotBeNull();
        discrepancy.PredecessorSessionId.ShouldBe(b.Id);
        discrepancy.CurrentTotalDelta.ShouldBe(10m);

        var attribute = await _client.PostAuthAsync(
            $"/session/opening-discrepancy/{discrepancy.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson
            {
                DiscrepancyRevision = discrepancy.CurrentRevision,
                Target = OpeningDiscrepancyAttribution.Incoming,
                Reason = "Conferido com o operador"
            },
            scenario.Manager.Token,
            expectedVersion: discrepancy.Version);
        attribute.StatusCode.ShouldBe(HttpStatusCode.OK, await attribute.Content.ReadAsStringAsync());
        (await QueueAsync(scenario)).Custody.OpeningDiscrepanciesAwaitingAttribution
            .ShouldNotContain(row => row.OpeningDiscrepancyId == discrepancy.Id);

        // Correcting B again supersedes the attributed revision: it stops counting and returns to the queue.
        await _client.CountAsync(b.Id, scenario.Manager.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 95m });
        var revised = (await (await _client.GetAuthAsync($"/session/{c.Id}/review", scenario.Manager.Token))
            .ReadContentAsync<ResponseDailyCloseReviewJson>()).OpeningDiscrepancy.ShouldNotBeNull();
        revised.CurrentRevision.ShouldBe(2);
        revised.CurrentTotalDelta.ShouldBe(5m);
        revised.AttributedRevision.ShouldBe(1);
        revised.CountsTowardPerson.ShouldBeFalse();
        var row = (await QueueAsync(scenario)).Custody.OpeningDiscrepanciesAwaitingAttribution
            .Single(item => item.OpeningDiscrepancyId == discrepancy.Id);
        row.Attribution.ShouldBe(OpeningDiscrepancyAttribution.Incoming);
        row.AttributedRevision.ShouldBe(1);
        row.CurrentRevision.ShouldBe(2);
    }

    [Fact]
    public async Task Replay_ShouldRecheckTheCallersCurrentCashboxGrant()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ReplayGrant");
        var opening = await _client.BuildOpeningAcknowledgmentAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        var request = new RequestStartSessionJson { AccountId = scenario.Cashbox.Id, Opening = opening };
        var key = $"replay-{Guid.NewGuid():N}";
        (await _client.PostAuthAsync("/session/start", request, scenario.MemberA.Token, idempotencyKey: key))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        await factory.ExecuteSqlAsync(
            $"UPDATE \"OperatorAccounts\" SET \"Active\" = false WHERE \"OperatorId\" = '{scenario.MemberA.Operator!.Id}'");
        var replay = await _client.PostAuthAsync("/session/start", request, scenario.MemberA.Token, idempotencyKey: key);

        replay.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<ResponseManagerQueueJson> QueueAsync(SessionScenario scenario)
    {
        var response = await _client.GetAuthAsync(
            $"/report/manager-queue?date={SessionFlow.LocalToday():yyyy-MM-dd}", scenario.Manager.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseManagerQueueJson>();
    }

    private async Task AcknowledgeAsync(DailyClose session, Guid sourceSessionId, Guid productId, decimal value)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var userId = await dbContext.Users.Select(user => user.Id).FirstAsync();
        var opening = new SessionOpening
        {
            BranchId = session.BranchId,
            SessionId = session.Id,
            EvidenceKind = SessionOpeningEvidenceKind.IncomingAcknowledgment,
            SourceKind = SessionOpeningSourceKind.PreviousSession,
            SourceId = sourceSessionId,
            SourceRevision = 0,
            AcknowledgedByUserId = userId,
            AcknowledgedAt = DateTime.UtcNow
        };
        opening.Items.Add(new SessionOpeningItem { SessionOpeningId = opening.Id, ProductId = productId, ProposedValue = value, AcknowledgedValue = value });
        dbContext.SessionOpenings.Add(opening);
        await dbContext.SaveChangesAsync();
    }

    private static DateTime SaoPaulo(DateTime businessDate, int hour, int minute, int second) =>
        DateTime.SpecifyKind(businessDate.Date.AddHours(hour + 3).AddMinutes(minute).AddSeconds(second), DateTimeKind.Utc);
}
