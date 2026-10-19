namespace server.Application.Services.Security;

public static class AccountSecurityOptionsFactory
{
    public static AccountSecurityOptions Create(string? applicationUrl, string? resetMinutes,
        string? invitationHours, string? recoveryCooldownSeconds)
    {
        var value = applicationUrl ?? "http://127.0.0.1:3000/";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || !value.EndsWith('/'))
            throw new InvalidOperationException("AccountSecurity:ApplicationUrl deve ser uma URL HTTPS confiável terminada em / (HTTP somente em loopback).");

        static int Bounded(string? configured, int fallback, int max, string key)
        {
            if (configured is null) return fallback;
            if (int.TryParse(configured, out var number) && number > 0 && number <= max) return number;
            throw new InvalidOperationException("Valor inválido em AccountSecurity:" + key);
        }

        return new AccountSecurityOptions(uri,
            TimeSpan.FromMinutes(Bounded(resetMinutes, 30, 60, "ResetMinutes")),
            TimeSpan.FromHours(Bounded(invitationHours, 72, 168, "InvitationHours")),
            TimeSpan.FromSeconds(Bounded(recoveryCooldownSeconds, 60, 3600, "RecoveryCooldownSeconds")));
    }
}
