using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>M9.6 Phase 3.1–3.4: the first operation starts the session through its own acknowledgment, atomically.</summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerInlineOpeningTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldRequireAnOpeningAcknowledgment_WhenNoSessionIsActive()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "InlineRequired");

        var response = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 50m, SessionFlow.LocalToday());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldHaveSingleItem().ShouldBe(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_REQUIRED);
        (await CountSessionsAsync(scenario)).ShouldBe(0);
    }

    [Fact]
    public async Task Create_ShouldStartTheSessionAndRecordTheOperationAtomically()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "InlineStart");
        var acknowledgment = await _client.BuildOpeningAcknowledgmentAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        var response = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 50m, SessionFlow.LocalToday(), acknowledgment);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.ReadContentAsync<ResponseCreateTransactionJson>();
        created.StartedSessionId.ShouldNotBeNull();
        created.SessionId.ShouldBe(created.StartedSessionId);
        var session = await _client.GetSessionAsync(created.StartedSessionId!.Value, scenario.MemberA.Token);
        session.Custody.CustodianOperatorId.ShouldBe(scenario.MemberA.Operator!.Id);
        (await factory.ReloadAsync<Transaction>(created.Id)).ShouldNotBeNull().SessionId.ShouldBe(session.Id);

        // The next operation binds to the active session; repeating the acknowledgment is a competing start.
        var next = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 10m, SessionFlow.LocalToday());
        next.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await next.ReadContentAsync<ResponseCreateTransactionJson>()).SessionId.ShouldBe(session.Id);
        var competing = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 10m, SessionFlow.LocalToday(), acknowledgment);
        competing.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await competing.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldHaveSingleItem().ShouldBe(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_NOT_ALLOWED);
    }

    [Fact]
    public async Task Create_ShouldLeaveNeitherSessionNorRow_WhenTheFirstOperationFails()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "InlineRollback");
        var acknowledgment = await _client.BuildOpeningAcknowledgmentAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();

        // The session and opening are already staged when the operation's own row insert fails.
        HttpResponseMessage response;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("INSERT INTO \"Transactions\"", StringComparison.Ordinal),
                   _ => throw new InvalidOperationException("Injected first-operation failure")))
        {
            response = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 50m, SessionFlow.LocalToday(), acknowledgment);
        }

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await CountSessionsAsync(scenario)).ShouldBe(0);
        var retry = await _client.PostTransactionAsync(scenario, scenario.MemberA.Token, 50m, SessionFlow.LocalToday(), acknowledgment);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created, await retry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_ShouldRefuseAMemberWhoIsNotTheActiveCustodian()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "InlineNotCustodian");
        await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);

        var response = await _client.PostTransactionAsync(scenario, scenario.MemberB.Token, 50m, SessionFlow.LocalToday());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<int> CountSessionsAsync(SessionScenario scenario)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.DailyCloses.CountAsync(close => close.AccountId == scenario.Cashbox.Id);
    }
}
