namespace server.Application.Services.Security;

public sealed record AccountSecurityOptions(Uri ApplicationUrl, TimeSpan ResetLifetime, TimeSpan InvitationLifetime, TimeSpan RecoveryCooldown)
{
    public string Link(string path, string token) => new Uri(ApplicationUrl, path).AbsoluteUri + "#token=" + Uri.EscapeDataString(token);
}
