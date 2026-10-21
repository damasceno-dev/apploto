using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// TestServer reports no remote address. This test-host-only filter runs ahead of the real pipeline and sets the
/// connection's remote address from <see cref="Header"/>, as Kestrel would report the socket peer, then removes the
/// header. Everything after it — forwarded-header trust, rate limits, login budgets — is the production pipeline.
/// </summary>
internal sealed class TestPeerStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Peer";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var value) && IPAddress.TryParse(value, out var address))
                context.Connection.RemoteIpAddress = address;
            context.Request.Headers.Remove(Header);
            return nextMiddleware(context);
        });
        next(app);
    };
}

internal static class TestPeerHttpExtensions
{
    public static Task<HttpResponseMessage> PostFromPeerAsync<T>(this HttpClient client, string url, T body, string peer,
        string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = System.Net.Http.Json.JsonContent.Create(body) };
        request.Headers.Add(TestPeerStartupFilter.Header, peer);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }
}
