namespace server.Application.Services.Security;

/// <summary>
/// Login abuse budgets (M8 Phase 8). <see cref="RequestsPerMinute"/> is the process-local per-client-network request
/// limit; the failure limits are the persistent per-(account, client network) and per-account budgets.
/// </summary>
public sealed record LoginProtectionOptions(
    int RequestsPerMinute,
    int ClientFailureLimit,
    int AccountFailureLimit,
    TimeSpan FailureWindow,
    TimeSpan Cooldown);
