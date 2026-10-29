using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>M9.6 Phase 2.3/2.5: shared-cashbox sequential sessions joined by an acknowledged handover.</summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerHandoverTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Handover_ShouldMoveCustody_AndKeepBothVectors_WhenTheIncomingCountDiffers()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "Handover");
        var outgoing = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await _client.CountAsync(outgoing.Session.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_100m });
        await _client.SubmitAsync(outgoing.Session.Id, scenario.MemberA.Token);
        var offer = await _client.OfferAsync(outgoing.Session.Id, scenario.MemberB.Operator!.Id, scenario.MemberA.Token);
        offer.State.ShouldBe(SessionHandoverState.Offered);
        offer.Items.ShouldHaveSingleItem().Value.ShouldBe(1_100m);

        // A pending offer keeps custody with the outgoing operator and freezes its ledger.
        (await _client.GetSessionAsync(outgoing.Session.Id, scenario.MemberA.Token)).Custody.CustodyState
            .ShouldBe(SessionCustodyState.Active);
        var whileOffered = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 5m, SessionFlow.LocalToday());
        whileOffered.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var accept = await _client.PostAuthAsync(
            $"/session/handover/{offer.Id}/accept",
            new RequestAcceptSessionHandoverJson
            {
                Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 1_080m }]
            },
            scenario.MemberB.Token,
            expectedVersion: offer.Version);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());
        var accepted = await accept.ReadContentAsync<ResponseAcceptSessionHandoverJson>();

        accepted.Handover.State.ShouldBe(SessionHandoverState.Accepted);
        accepted.OutgoingSession.Custody.CustodyState.ShouldBe(SessionCustodyState.HandedOver);
        accepted.OutgoingSession.Status.ShouldBe(DailyCloseStatus.Submitted);
        var incoming = accepted.Incoming.Session;
        incoming.Custody.CustodyState.ShouldBe(SessionCustodyState.Active);
        incoming.Custody.CustodianOperatorId.ShouldBe(scenario.MemberB.Operator!.Id);
        incoming.Custody.Sequence.ShouldBe(2);
        incoming.Custody.StartedAt.ShouldBe(accepted.OutgoingSession.Custody.EndedAt);
        var discrepancy = accepted.Incoming.OpeningDiscrepancy.ShouldNotBeNull();
        discrepancy.CurrentTotalDelta.ShouldBe(-20m);
        discrepancy.HandoverId.ShouldBe(offer.Id);
        discrepancy.Attribution.ShouldBe(OpeningDiscrepancyAttribution.Unattributed);

        // The work queue shows the unattributed discrepancy; the manager attributes the exact revision.
        var queue = await (await _client.GetAuthAsync($"/report/manager-queue?date={SessionFlow.LocalToday():yyyy-MM-dd}", scenario.Manager.Token))
            .ReadContentAsync<ResponseManagerQueueJson>();
        queue.Custody.OpeningDiscrepanciesAwaitingAttribution.ShouldContain(row => row.OpeningDiscrepancyId == discrepancy.Id);
        queue.Custody.OpenSessions.ShouldContain(row => row.SessionId == incoming.Id);

        var attribution = await _client.PostAuthAsync(
            $"/session/opening-discrepancy/{discrepancy.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson
            {
                DiscrepancyRevision = discrepancy.CurrentRevision,
                Target = OpeningDiscrepancyAttribution.Outgoing,
                Reason = "Contagem do turno anterior conferida"
            },
            scenario.Manager.Token,
            expectedVersion: discrepancy.Version);
        attribution.StatusCode.ShouldBe(HttpStatusCode.OK, await attribution.Content.ReadAsStringAsync());
        var attributed = await attribution.ReadContentAsync<ResponseOpeningDiscrepancyJson>();
        attributed.Attribution.ShouldBe(OpeningDiscrepancyAttribution.Outgoing);
        attributed.CountsTowardPerson.ShouldBeTrue();

        // The incoming operator works under the acknowledged opening.
        var operation = await _client.PostTransactionAsync(scenario, scenario.MemberB.Token, 30m, SessionFlow.LocalToday());
        operation.StatusCode.ShouldBe(HttpStatusCode.Created, await operation.Content.ReadAsStringAsync());
        (await operation.ReadContentAsync<ResponseCreateTransactionJson>()).SessionId.ShouldBe(incoming.Id);

        var history = await (await _client.GetAuthAsync($"/session/{outgoing.Session.Id}/history", scenario.Manager.Token))
            .ReadContentAsync<ResponseDailyCloseHistoryJson>();
        history.CustodyEvents.Select(e => e.Kind).ShouldContain(SessionEventKind.HandoverOffered);
        history.CustodyEvents.Select(e => e.Kind).ShouldContain(SessionEventKind.HandoverAccepted);
        history.Handovers.ShouldHaveSingleItem().State.ShouldBe(SessionHandoverState.Accepted);
    }

    [Fact]
    public async Task Handover_ShouldOnlyBeAcceptedByTheIntendedIncomingOperator()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "HandoverIntended");
        var outgoing = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await _client.CountAsync(outgoing.Session.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 10m });
        await _client.SubmitAsync(outgoing.Session.Id, scenario.MemberA.Token);
        var offer = await _client.OfferAsync(outgoing.Session.Id, scenario.MemberB.Operator!.Id, scenario.MemberA.Token);

        var byOutgoing = await _client.PostAuthAsync(
            $"/session/handover/{offer.Id}/accept",
            new RequestAcceptSessionHandoverJson { Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 10m }] },
            scenario.MemberA.Token,
            expectedVersion: offer.Version);

        byOutgoing.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Recall_ShouldWithdrawThePendingOffer_SoAChangedCountIsNeverAcceptedSilently()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "HandoverRecall");
        var outgoing = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await _client.CountAsync(outgoing.Session.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 10m });
        await _client.SubmitAsync(outgoing.Session.Id, scenario.MemberA.Token);
        var offer = await _client.OfferAsync(outgoing.Session.Id, scenario.MemberB.Operator!.Id, scenario.MemberA.Token);

        var recall = await _client.PostAuthAsync($"/session/{outgoing.Session.Id}/recall", scenario.MemberA.Token);
        recall.StatusCode.ShouldBe(HttpStatusCode.OK, await recall.Content.ReadAsStringAsync());

        var accept = await _client.PostAuthAsync(
            $"/session/handover/{offer.Id}/accept",
            new RequestAcceptSessionHandoverJson { Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 10m }] },
            scenario.MemberB.Token,
            expectedVersion: offer.Version);
        accept.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await accept.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldHaveSingleItem()
            .ShouldBeOneOf(ResourcesErrorMessages.SESSION_HANDOVER_NOT_PENDING, ResourcesErrorMessages.SESSION_HANDOVER_STALE);
    }

    [Fact]
    public async Task Finish_ShouldRequireASubmittedCount()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "FinishDraft");
        var started = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        var response = await _client.PostAuthAsync(
            $"/session/{started.Session.Id}/finish",
            new RequestFinishSessionJson(),
            scenario.MemberA.Token,
            expectedVersion: started.Session.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldHaveSingleItem()
            .ShouldBe(ResourcesErrorMessages.SESSION_FINISH_REQUIRES_SUBMITTED);
    }
}
