using System.Globalization;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Headers;

internal static class EntityTagHeader
{
    public const string IfMatchName = "If-Match";

    public static uint ParseRequired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_REQUIRED]);

        if (value.Length < 3 || value[0] != '"' || value[^1] != '"' ||
            !uint.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
            version == 0)
        {
            throw new OnValidationException([ResourcesErrorMessages.CONCURRENCY_IF_MATCH_INVALID]);
        }

        return version;
    }

    public static void Set(HttpResponse response, uint version)
    {
        response.Headers.ETag = Format(version);
    }

    public static string Format(uint version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";
}
