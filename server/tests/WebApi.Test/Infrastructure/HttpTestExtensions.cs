using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;

namespace WebApi.Test.Infrastructure;

internal static class HttpTestExtensions
{
    extension(HttpClient client)
    {
        public Task<HttpResponseMessage> GetAuthAsync(string requestUri,
            string token)
        {
            return SendAuthAsync(client, HttpMethod.Get, requestUri, null, token);
        }

        public Task<HttpResponseMessage> PostAuthAsync<TRequest>(string requestUri,
            TRequest request,
            string token)
        {
            return SendAuthAsync(client, HttpMethod.Post, requestUri, JsonContent.Create(request), token);
        }

        public Task<HttpResponseMessage> PostAuthAsync(string requestUri,
            string token)
        {
            return SendAuthAsync(client, HttpMethod.Post, requestUri, null, token);
        }

        public Task<HttpResponseMessage> PutAuthAsync<TRequest>(string requestUri,
            TRequest request,
            string token)
        {
            return SendAuthAsync(client, HttpMethod.Put, requestUri, JsonContent.Create(request), token);
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
            var payload = await response.Content.ReadFromJsonAsync<TResponse>();
            payload.ShouldNotBeNull();
            return payload;
        }
    }

    private static async Task<HttpResponseMessage> SendAuthAsync(HttpClient client,
        HttpMethod method,
        string requestUri,
        HttpContent? content,
        string token)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
