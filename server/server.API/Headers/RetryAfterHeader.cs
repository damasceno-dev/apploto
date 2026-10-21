namespace server.Headers;

/// <summary>Every 429 carries <c>Retry-After</c> as whole seconds, rounded up and never below one.</summary>
public static class RetryAfterHeader
{
    public const string Name = "Retry-After";

    public static void Write(HttpResponse response, TimeSpan retryAfter) =>
        response.Headers.RetryAfter = Math.Max(1, (long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
