using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using server.Communication.Requests;
using Shouldly;

namespace WebApi.Test.Infrastructure;

internal static class HttpTestExtensions
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();

    extension(HttpClient client)
    {
        public Task<HttpResponseMessage> GetAuthAsync(string requestUri,
            string token)
        {
            return SendAuthAsync(client, HttpMethod.Get, requestUri, null, token);
        }

        public Task<HttpResponseMessage> PostAuthAsync<TRequest>(string requestUri,
            TRequest request,
            string token,
            string? idempotencyKey = null,
            uint? expectedVersion = null)
        {
            if (idempotencyKey is null && requestUri is "/transaction" or "/transaction/installment")
                idempotencyKey = $"test-{Guid.NewGuid():N}";

            return SendAuthAsync(
                client,
                HttpMethod.Post,
                requestUri,
                JsonContent.Create(request),
                token,
                idempotencyKey,
                expectedVersion);
        }

        public Task<HttpResponseMessage> PostAuthAsync(string requestUri,
            string token,
            uint? expectedVersion = null)
        {
            return SendAuthAsync(
                client,
                HttpMethod.Post,
                requestUri,
                null,
                token,
                expectedVersion: expectedVersion);
        }

        public Task<HttpResponseMessage> PutAuthAsync<TRequest>(string requestUri,
            TRequest request,
            string token,
            uint? expectedVersion = null)
        {
            if (expectedVersion is null && request is VersionedRequestPutDailyCloseItemsJson closeItems)
                expectedVersion = closeItems.Version;

            return SendAuthAsync(
                client,
                HttpMethod.Put,
                requestUri,
                JsonContent.Create(request),
                token,
                expectedVersion: expectedVersion);
        }

        public Task<HttpResponseMessage> DeleteAuthAsync(string requestUri,
            string token)
        {
            return SendAuthAsync(client, HttpMethod.Delete, requestUri, null, token);
        }
    }

    extension(HttpResponseMessage response)
    {
        public async Task<TResponse> ReadContentAsync<TResponse>()
            where TResponse : class
        {
            var payload = await response.Content.ReadFromJsonAsync<TResponse>(ResponseJsonOptions);
            payload.ShouldNotBeNull();
            return payload;
        }
    }

    private static async Task<HttpResponseMessage> SendAuthAsync(HttpClient client,
        HttpMethod method,
        string requestUri,
        HttpContent? content,
        string token,
        string? idempotencyKey = null,
        uint? expectedVersion = null)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is not null)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (expectedVersion is not null)
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{expectedVersion}\"");
        return await client.SendAsync(request);
    }

    private static JsonSerializerOptions CreateResponseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal sealed class VersionedRequestPutDailyCloseItemsJson : RequestPutDailyCloseItemsJson
{
    [JsonIgnore]
    public uint Version { get; init; }
}
