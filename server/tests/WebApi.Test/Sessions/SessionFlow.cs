using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using Shouldly;
using WebApi.Test.Infrastructure;

namespace WebApi.Test.Sessions;

/// <summary>Thin API steps shared by the session journeys.</summary>
internal static class SessionFlow
{
    extension(HttpClient client)
    {
        public async Task<ResponseDailyCloseJson> GetSessionAsync(Guid sessionId, string token)
        {
            var response = await client.GetAuthAsync($"/session/{sessionId}", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseDailyCloseJson>();
        }

        public async Task<ResponseDailyCloseJson> CountAsync(Guid sessionId, string token, IReadOnlyDictionary<Guid, decimal> values)
        {
            var current = await client.GetSessionAsync(sessionId, token);
            var response = await client.PutAuthAsync(
                $"/session/{sessionId}/items",
                new VersionedRequestPutDailyCloseItemsJson
                {
                    Version = current.Version,
                    Items = values.Select(pair => new RequestUpsertDailyCloseItemJson { ProductId = pair.Key, Value = pair.Value }).ToList()
                },
                token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await response.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
        }

        public async Task<ResponseDailyCloseJson> SubmitAsync(Guid sessionId, string token)
        {
            var response = await client.PostAuthAsync($"/session/{sessionId}/submit", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseDailyCloseJson>();
        }

        public async Task<ResponseDailyCloseJson> FinishAsync(Guid sessionId, string token, string? reason = null)
        {
            var current = await client.GetSessionAsync(sessionId, token);
            var response = await client.PostAuthAsync(
                $"/session/{sessionId}/finish",
                new RequestFinishSessionJson { Reason = reason },
                token,
                expectedVersion: current.Version);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseDailyCloseJson>();
        }

        public async Task<ResponseSessionHandoverJson> OfferAsync(Guid sessionId, Guid incomingOperatorId, string token)
        {
            var current = await client.GetSessionAsync(sessionId, token);
            var response = await client.PostAuthAsync(
                $"/session/{sessionId}/handover",
                new RequestOfferSessionHandoverJson
                {
                    IncomingOperatorId = incomingOperatorId,
                    CountRevision = current.Custody.CountRevision
                },
                token,
                expectedVersion: current.Version);
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseSessionHandoverJson>();
        }

        public Task<HttpResponseMessage> PostTransactionAsync(SessionScenario scenario, string token, decimal value, DateTime date,
            RequestSessionOpeningAcknowledgmentJson? acknowledgment = null, Guid? sessionId = null, string? reason = null,
            Guid? recordedByOperatorId = null)
        {
            return client.PostAuthAsync(
                "/transaction",
                new CommonTestUtilities.Requests.RequestCreateTransactionJsonBuilder()
                    .WithAccountId(scenario.Cashbox.Id)
                    .WithTransactionTypeId(scenario.InType.Id)
                    .WithDate(date)
                    .WithValue(value)
                    .WithDueDate(null)
                    .WithOpeningAcknowledgment(acknowledgment)
                    .WithSession(sessionId, reason)
                    .WithRecordedByOperatorId(recordedByOperatorId)
                    .Build(),
                token);
        }
    }

    public static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
