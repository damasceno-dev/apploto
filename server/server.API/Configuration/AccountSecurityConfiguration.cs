using server.Application.Services.Security;

namespace server.Configuration;

internal static class AccountSecurityConfiguration
{
    public static void AddAccountSecurityConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var value = configuration["AccountSecurity:ApplicationUrl"] ?? "http://127.0.0.1:3000/";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || !value.EndsWith('/'))
            throw new InvalidOperationException("AccountSecurity:ApplicationUrl must be a trusted HTTPS base URL ending in / (HTTP only for loopback).");
        static int Bounded(IConfiguration c, string key, int fallback, int max)
        {
            var number = c.GetValue<int?>("AccountSecurity:" + key) ?? fallback;
            return number > 0 && number <= max ? number : throw new InvalidOperationException("Invalid AccountSecurity:" + key);
        }
        services.AddSingleton(new AccountSecurityOptions(uri,
            TimeSpan.FromMinutes(Bounded(configuration, "ResetMinutes", 30, 60)),
            TimeSpan.FromHours(Bounded(configuration, "InvitationHours", 72, 168)),
            TimeSpan.FromSeconds(Bounded(configuration, "RecoveryCooldownSeconds", 60, 3600))));
    }
}
