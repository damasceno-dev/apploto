using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

[Collection(ServerApiCollection.Name)]
public class SessionControllerStartHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Start_ShouldOpenActiveCustody_FromAnExplicitZeroAcknowledgment()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartZero");

        var started = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        var custody = started.Session.Custody;
        custody.CustodyState.ShouldBe(SessionCustodyState.Active);
        custody.MappingState.ShouldBe(SessionMappingState.Ready);
        custody.Sequence.ShouldBe(1);
        custody.CustodianOperatorId.ShouldBe(scenario.MemberA.Operator!.Id);
        custody.TerminalId.ShouldBe(scenario.Cashbox.TerminalId);
        custody.OpeningAcknowledged.ShouldBeTrue();
        started.Session.Status.ShouldBe(DailyCloseStatus.Draft);
        started.Session.Date.ShouldBe(SessionFlow.LocalToday());
        started.OpeningDiscrepancy.ShouldBeNull();

        var opening = (await factory.ReloadAsync<SessionOpening>(started.SessionOpeningId)).ShouldNotBeNull();
        opening.SourceKind.ShouldBe(SessionOpeningSourceKind.ExplicitZero);
        opening.EvidenceKind.ShouldBe(SessionOpeningEvidenceKind.IncomingAcknowledgment);
    }

    [Fact]
    public async Task Start_ShouldProposeThePreviousCountedClosing_AndRecordANonzeroDifferenceAsUnattributed()
    {
        // E11b: Finished 1,100 → Start 1,080 creates −20 against the incoming opening, with the finished session as the
        // evidenced predecessor and no fabricated handover.
        var scenario = await SessionScenario.CreateAsync(factory, "StartDiff");
        var first = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await _client.CountAsync(first.Session.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_100m });
        await _client.SubmitAsync(first.Session.Id, scenario.MemberA.Token);
        await _client.FinishAsync(first.Session.Id, scenario.MemberA.Token);

        var proposal = await (await _client.GetAuthAsync(
            $"/session/opening-proposal?accountId={scenario.Cashbox.Id}", scenario.MemberB.Token))
            .ReadContentAsync<ResponseSessionOpeningProposalJson>();
        proposal.SourceKind.ShouldBe(SessionOpeningSourceKind.PreviousSession);
        proposal.SourceId.ShouldBe(first.Session.Id);
        proposal.Items.Single(item => item.ProductId == scenario.Cash.Id).Value.ShouldBe(1_100m);
        proposal.CanStart.ShouldBeTrue();

        var second = await _client.StartSessionAsync(
            scenario.Cashbox.Id,
            scenario.MemberB.Token,
            new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_080m });

        second.Session.Custody.Sequence.ShouldBe(2);
        var discrepancy = second.OpeningDiscrepancy.ShouldNotBeNull();
        discrepancy.CurrentTotalDelta.ShouldBe(-20m);
        discrepancy.Attribution.ShouldBe(OpeningDiscrepancyAttribution.Unattributed);
        discrepancy.HandoverId.ShouldBeNull();
        discrepancy.PredecessorSessionId.ShouldBe(first.Session.Id);
        discrepancy.CountsTowardPerson.ShouldBeFalse();
        discrepancy.AllowedTargets.ShouldBe([
            OpeningDiscrepancyAttribution.Outgoing,
            OpeningDiscrepancyAttribution.Incoming,
            OpeningDiscrepancyAttribution.Neither
        ]);

        // The new session opens with the real 1,080: variance zero before any work.
        await _client.CountAsync(second.Session.Id, scenario.MemberB.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_080m });
        var submitted = await _client.SubmitAsync(second.Session.Id, scenario.MemberB.Token);
        submitted.Items.Single(item => item.ProductId == scenario.CashVariance.Id).Value.ShouldBe(0m);
    }

    [Fact]
    public async Task Start_ShouldProposeTheOpeningCount_WhenTheCashboxHasNoCountedSession()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartOpeningCount");
        var record = await _client.PostAuthAsync(
            "/session/opening-count",
            new RequestRecordOpeningCountJson
            {
                AccountId = scenario.Cashbox.Id,
                AsOfDate = SessionFlow.LocalToday(),
                Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 500m }]
            },
            scenario.Manager.Token);
        record.StatusCode.ShouldBe(HttpStatusCode.OK, await record.Content.ReadAsStringAsync());
        var openingCount = await record.ReadContentAsync<ResponseOpeningCountJson>();
        openingCount.Revision.ShouldBe(1);

        var started = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        started.OpeningDiscrepancy.ShouldBeNull();
        var opening = (await factory.ReloadAsync<SessionOpening>(started.SessionOpeningId)).ShouldNotBeNull();
        opening.SourceKind.ShouldBe(SessionOpeningSourceKind.OpeningCount);
        opening.SourceId.ShouldBe(openingCount.Id);

        // The opening count carries no variance and becomes read-only once a counted session exists.
        await _client.CountAsync(started.Session.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 500m });
        var again = await _client.PostAuthAsync(
            "/session/opening-count",
            new RequestRecordOpeningCountJson
            {
                AccountId = scenario.Cashbox.Id,
                AsOfDate = SessionFlow.LocalToday(),
                Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 600m }],
                ExpectedRevision = 1
            },
            scenario.Manager.Token);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Start_ShouldReplayTheOriginalOutcome_ForTheSameIdempotencyKey()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartReplay");
        var opening = await _client.BuildOpeningAcknowledgmentAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        var request = new RequestStartSessionJson { AccountId = scenario.Cashbox.Id, Opening = opening };
        var key = $"start-{Guid.NewGuid():N}";

        var first = await _client.PostAuthAsync("/session/start", request, scenario.MemberA.Token, idempotencyKey: key);
        var replay = await _client.PostAuthAsync("/session/start", request, scenario.MemberA.Token, idempotencyKey: key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await replay.ReadContentAsync<ResponseStartSessionJson>()).Session.Id
            .ShouldBe((await first.ReadContentAsync<ResponseStartSessionJson>()).Session.Id);
    }

    [Fact]
    public async Task Start_ShouldLetSeparateCashboxesOfOneTerminalHoldParallelSessions()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartParallel");
        var secondCashbox = await scenario.SecondCashboxAsync(factory, scenario.Cashbox.TerminalId!.Value);

        var a = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        var b = await _client.StartSessionAsync(secondCashbox.Id, scenario.MemberB.Token);

        a.Session.Custody.TerminalId.ShouldBe(b.Session.Custody.TerminalId);
        a.Session.Custody.CustodyState.ShouldBe(SessionCustodyState.Active);
        b.Session.Custody.CustodyState.ShouldBe(SessionCustodyState.Active);
        b.Session.Custody.Sequence.ShouldBe(1);
    }
}
