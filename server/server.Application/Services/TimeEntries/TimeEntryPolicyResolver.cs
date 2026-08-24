using server.Domain.Entities;

namespace server.Application.Services.TimeEntries;

/// <summary>
/// Single source of truth for per-date policy resolution (§6.7): the applicable
/// <see cref="TimeEntryPolicy"/> for an entry date is the row with the greatest
/// <c>EffectiveFrom</c> that is on or before that date. Every calculation caller —
/// writes, reads, and reports — resolves through this helper over the branch's active
/// policy rows so a settings change effective today never rewrites earlier balances.
/// </summary>
public static class TimeEntryPolicyResolver
{
    /// <summary>
    /// Resolves the policy applicable to <paramref name="entryDate"/> from the branch's
    /// active policy rows. Ties on <c>EffectiveFrom</c> are impossible under the active
    /// unique index; the CreatedAt/Id tie-breakers keep in-memory resolution deterministic
    /// anyway. A branch always has an initial row dated <see cref="DateTime.MinValue"/>
    /// (seeded on branch create, backfilled by migration), so a miss is a system
    /// invariant breach, not a user error.
    /// </summary>
    public static TimeEntryPolicy Resolve(IReadOnlyList<TimeEntryPolicy> policies, DateTime entryDate)
    {
        return policies
                .Where(policy => policy.EffectiveFrom <= entryDate.Date)
                .OrderByDescending(policy => policy.EffectiveFrom)
                .ThenByDescending(policy => policy.CreatedAt)
                .ThenByDescending(policy => policy.Id)
                .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"TimeEntryPolicy row missing for date {entryDate:yyyy-MM-dd}.");
    }
}
