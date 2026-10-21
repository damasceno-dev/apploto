namespace server.Application.Services.Security;

public static class LoginProtectionOptionsFactory
{
    internal const int DefaultRequestsPerMinute = 30;
    internal const int MaxRequestsPerMinute = 1000;
    internal const int DefaultClientFailureLimit = 5;
    internal const int MaxClientFailureLimit = 100;
    internal const int DefaultAccountFailureLimit = 20;
    internal const int MaxAccountFailureLimit = 1000;
    internal const int DefaultFailureWindowMinutes = 15;
    internal const int DefaultCooldownMinutes = 15;
    internal const int MaxMinutes = 1440;

    public static LoginProtectionOptions Create(string? requestsPerMinute, string? clientFailureLimit,
        string? accountFailureLimit, string? failureWindowMinutes, string? cooldownMinutes)
    {
        var client = Bounded(clientFailureLimit, DefaultClientFailureLimit, 1, MaxClientFailureLimit, "LoginClientFailureLimit");
        // The per-account budget spans every client network, so it can never be tighter than one network's budget.
        var account = Bounded(accountFailureLimit, DefaultAccountFailureLimit, client, MaxAccountFailureLimit, "LoginAccountFailureLimit");
        return new LoginProtectionOptions(
            Bounded(requestsPerMinute, DefaultRequestsPerMinute, 1, MaxRequestsPerMinute, "LoginRequestsPerMinute"),
            client,
            account,
            TimeSpan.FromMinutes(Bounded(failureWindowMinutes, DefaultFailureWindowMinutes, 1, MaxMinutes, "LoginFailureWindowMinutes")),
            TimeSpan.FromMinutes(Bounded(cooldownMinutes, DefaultCooldownMinutes, 1, MaxMinutes, "LoginCooldownMinutes")));
    }

    private static int Bounded(string? configured, int fallback, int min, int max, string key)
    {
        // A default obeys the same bounds as a configured value: a network budget above the default account budget
        // must be paired with an explicit account budget, never silently undercut by the default.
        var number = fallback;
        if ((configured is null || int.TryParse(configured, out number)) && number >= min && number <= max) return number;
        throw new InvalidOperationException($"Valor inválido em AccountSecurity:{key} (permitido {min}–{max}).");
    }
}
