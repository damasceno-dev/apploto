using System.Net;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

[Collection(ServerApiCollection.Name)]
public class SessionControllerStartUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Start_ShouldRefuseAMissingProposal_WithoutAnExplicitZero()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartNoZero");
        var request = new RequestStartSessionJson
        {
            AccountId = scenario.Cashbox.Id,
            Opening = new RequestSessionOpeningAcknowledgmentJson { ExplicitZero = false, Items = [] }
        };

        var response = await _client.PostAuthAsync("/session/start", request, scenario.MemberA.Token);

        await AssertConflict(response, ResourcesErrorMessages.SESSION_OPENING_SOURCE_MISSING);
    }

    [Fact]
    public async Task Start_ShouldRefuseAStaleProposal()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartStale");
        var request = new RequestStartSessionJson
        {
            AccountId = scenario.Cashbox.Id,
            Opening = new RequestSessionOpeningAcknowledgmentJson
            {
                SourceKind = SessionOpeningSourceKind.PreviousSession,
                SourceId = Guid.NewGuid(),
                SourceRevision = 1,
                Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 10m }]
            }
        };

        var response = await _client.PostAuthAsync("/session/start", request, scenario.MemberA.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Start_ShouldRefuseASecondCustody_OnTheSameCashbox()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartTwice");
        await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        var response = await _client.PostStartSessionAsync(scenario.Cashbox.Id, scenario.MemberB.Token);

        await AssertConflict(response, ResourcesErrorMessages.SESSION_ACTIVE_CUSTODY_CONFLICT);
    }

    [Fact]
    public async Task Start_ShouldResolveConcurrentStartsToOneActiveCustody()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartRace");
        var first = await _client.BuildOpeningAcknowledgmentAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        var second = await _client.BuildOpeningAcknowledgmentAsync(scenario.Cashbox.Id, scenario.MemberB.Token);
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();

        var responses = await Task.WhenAll(
            clientA.PostAuthAsync("/session/start", new RequestStartSessionJson { AccountId = scenario.Cashbox.Id, Opening = first }, scenario.MemberA.Token),
            clientB.PostAuthAsync("/session/start", new RequestStartSessionJson { AccountId = scenario.Cashbox.Id, Opening = second }, scenario.MemberB.Token));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
    }

    [Fact]
    public async Task Start_ShouldRefuseAMemberStartingForAnotherCustodian()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartOther");

        var response = await _client.PostStartSessionAsync(
            scenario.Cashbox.Id,
            scenario.MemberA.Token,
            custodianOperatorId: scenario.MemberB.Operator!.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Start_ShouldRequireAReason_WhenAManagerStartsForAnotherCustodian()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartManagerReason");

        var withoutReason = await _client.PostStartSessionAsync(
            scenario.Cashbox.Id,
            scenario.Manager.Token,
            custodianOperatorId: scenario.MemberA.Operator!.Id);
        withoutReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var withReason = await _client.PostStartSessionAsync(
            scenario.Cashbox.Id,
            scenario.Manager.Token,
            custodianOperatorId: scenario.MemberA.Operator!.Id,
            reason: "Operador sem celular");
        withReason.StatusCode.ShouldBe(HttpStatusCode.Created, await withReason.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Start_ShouldRequireAPhysicalTerminal()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "StartNoTerminal");
        await factory.ExecuteSqlAsync($"UPDATE \"Accounts\" SET \"TerminalId\" = NULL WHERE \"Id\" = '{scenario.Cashbox.Id}'");

        var response = await _client.PostStartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        await AssertConflict(response, ResourcesErrorMessages.SESSION_CASHBOX_TERMINAL_REQUIRED);
    }

    private static async Task AssertConflict(HttpResponseMessage response, string message)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldHaveSingleItem().ShouldBe(message);
    }
}
