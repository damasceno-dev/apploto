using System.Net;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using WebApi.Test.Infrastructure;

namespace WebApi.Test.Sessions;

/// <summary>
/// Blind-count fixtures (M9.6 Phase 4): a cashbox whose first session acknowledged a 1,000 cash opening recorded by an
/// opening count, worked by Member A, and the persisted attempt/event evidence read through the repositories.
/// </summary>
internal static class SessionCountScenario
{
    public const decimal Opening = 1_000m;

    public static async Task<(SessionScenario Scenario, ResponseDailyCloseJson Session)> StartWithOpeningAsync(
        ServerWebApplicationFactory factory,
        HttpClient client,
        string label)
    {
        var scenario = await SessionScenario.CreateAsync(factory, label);
        var openingCount = await client.PostAuthAsync(
            "/session/opening-count",
            new RequestRecordOpeningCountJson
            {
                AccountId = scenario.Cashbox.Id,
                AsOfDate = SessionFlow.LocalToday(),
                Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = Opening }]
            },
            scenario.Manager.Token);
        openingCount.StatusCode.ShouldBe(HttpStatusCode.OK, await openingCount.Content.ReadAsStringAsync());
        var started = await client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        return (scenario, started.Session);
    }

    public static async Task<ResponseCreateTransactionJson> RecordInflowAsync(
        HttpClient client,
        SessionScenario scenario,
        string token,
        decimal value)
    {
        var response = await client.PostTransactionAsync(scenario, token, value, SessionFlow.LocalToday());
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseCreateTransactionJson>();
    }

    public static async Task<HttpResponseMessage> SaveCountAsync(
        HttpClient client,
        Guid sessionId,
        string token,
        IReadOnlyDictionary<Guid, decimal> values,
        string? notes = null)
    {
        var current = (await client.TryGetSessionAsync(sessionId, token)).ShouldNotBeNull();
        return await client.PutAuthAsync(
            $"/session/{sessionId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = current.Version,
                Notes = notes,
                Items = values.Select(pair => new RequestUpsertDailyCloseItemJson { ProductId = pair.Key, Value = pair.Value }).ToList()
            },
            token);
    }

    public static async Task<IReadOnlyList<SessionCountAttempt>> AttemptsAsync(ServerWebApplicationFactory factory, Guid branchId, Guid sessionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISessionCountAttemptsRepository>()
            .ListBySessionIdAsNoTracking(branchId, sessionId);
    }

    public static async Task<IReadOnlyList<SessionEvent>> EventsAsync(ServerWebApplicationFactory factory, Guid branchId, Guid sessionId, SessionEventKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var events = await scope.ServiceProvider.GetRequiredService<ISessionEventsRepository>()
            .ListBySessionIdAsNoTracking(branchId, sessionId);
        return events.Where(sessionEvent => sessionEvent.Kind == kind).ToList();
    }
}
