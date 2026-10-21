using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Security;

/// <summary>
/// Persistent failed-password budgets (M8 Phase 8): one per canonical account and one per account seen from one client
/// network. Call it only inside the caller's <see cref="SecurityRules.EmailKey"/> coordination scope, so reading,
/// checking and writing both rows is serialized per account and commits with the caller's transaction. Missing,
/// inactive, passwordless and existing accounts take exactly the same path.
/// </summary>
public sealed class LoginAttemptGuard(ILoginThrottleRepository throttles, IClientNetworkProvider client,
    LoginProtectionOptions options, TimeProvider time)
{
    public static string AccountKey(string email) => SecurityRules.Hash("login-account:" + email);
    private static string ClientKey(string email, string network) => SecurityRules.Hash("login-client:" + email + "\n" + network);

    /// <summary>Throws 429 before any account read or credential check while either budget is blocked.</summary>
    public async Task<LoginAttempt> Admit(string email)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var accountKey = AccountKey(email);
        var clientKey = ClientKey(email, client.Current());
        var rows = await throttles.GetForUpdate(accountKey, clientKey);
        var account = rows.SingleOrDefault(r => r.KeyHash == accountKey);
        var pair = rows.SingleOrDefault(r => r.KeyHash == clientKey);
        var wait = Max(Remaining(account, now), Remaining(pair, now));
        if (wait is { } retryAfter)
            throw new TooManyAttemptsException(ResourcesErrorMessages.SECURITY_RATE_LIMITED, retryAfter);
        return new LoginAttempt(accountKey, clientKey, account, pair, now);
    }

    /// <summary>Counts one failed credential check against both budgets; the caller commits before answering.</summary>
    public async Task Failed(LoginAttempt attempt)
    {
        await Count(attempt.Account, attempt.AccountKey, attempt, options.AccountFailureLimit);
        await Count(attempt.Client, attempt.ClientKey, attempt, options.ClientFailureLimit);
    }

    /// <summary>
    /// A proven password clears only this client network's budget. The account budget runs out on its own, so a
    /// legitimate sign-in never refills the budget of a distributed guesser.
    /// </summary>
    public void Succeeded(LoginAttempt attempt)
    {
        if (attempt.Client is not null) throttles.Remove(attempt.Client);
    }

    private async Task Count(LoginThrottle? row, string key, LoginAttempt attempt, int limit)
    {
        var now = attempt.Now;
        if (row is null)
        {
            row = new LoginThrottle { KeyHash = key, AccountKeyHash = attempt.AccountKey, CreatedAt = now, WindowStartedAt = now };
            await throttles.Add(row);
        }
        else if (row.BlockedUntil.HasValue || row.WindowStartedAt.Add(options.FailureWindow) <= now)
        {
            // Admit refused every still-blocked row, so a block or window seen here has ended: start a new window.
            row.FailureCount = 0;
            row.WindowStartedAt = now;
            row.BlockedUntil = null;
        }

        row.FailureCount++;
        if (row.FailureCount >= limit) row.BlockedUntil = now.Add(options.Cooldown);
        row.ExpiresAt = row.BlockedUntil ?? row.WindowStartedAt.Add(options.FailureWindow);
    }

    private static TimeSpan? Remaining(LoginThrottle? row, DateTime now) =>
        row?.BlockedUntil is { } until && until > now ? until - now : null;

    private static TimeSpan? Max(TimeSpan? left, TimeSpan? right) =>
        left is null ? right : right is null ? left : left > right ? left : right;
}

public sealed record LoginAttempt(
    string AccountKey,
    string ClientKey,
    LoginThrottle? Account,
    LoginThrottle? Client,
    DateTime Now);
