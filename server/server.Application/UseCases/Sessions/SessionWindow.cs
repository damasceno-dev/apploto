using server.Application.Services.Transactions;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions;

/// <summary>
/// A manager-declared past interval (owner decision F2): half-open, wholly in the past and inside one branch-local
/// business day. The database enforces the same window and the non-overlap; this check only fails early.
/// </summary>
internal static class SessionWindow
{
    public static (DateTime StartedAt, DateTime EndedAt, DateTime BusinessDate) Resolve(
        DateTime startedAt,
        DateTime endedAt,
        IBranchClock branchClock)
    {
        var start = AsUtc(startedAt);
        var end = AsUtc(endedAt);
        var businessDate = branchClock.LocalBusinessDate(start);
        var endBusinessDate = branchClock.LocalBusinessDate(end.AddTicks(-1));

        if (end <= start || end > branchClock.UtcNow() || endBusinessDate != businessDate)
            throw new OnValidationException([ResourcesErrorMessages.SESSION_DECLARE_WINDOW_INVALID]);

        return (start, end, businessDate);
    }

    public static bool Overlaps(DateTime startA, DateTime? endA, DateTime startB, DateTime? endB)
    {
        return startA < (endB ?? DateTime.MaxValue) && startB < (endA ?? DateTime.MaxValue);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
