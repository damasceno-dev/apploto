using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Receivables;

/// <summary>The dates of one new settlement event after validation.</summary>
public sealed record SettlementEventDates(DateTime OccurredOn, DateTime PostedOn, DateTime Today);

/// <summary>
/// The date rules of every settlement command (M9.7 Phase 1.2), read from the branch clock and the lock inside the
/// command's coordination: <c>OccurredOn</c> is the real date and may lie in a locked period, never in the future;
/// <c>PostedOn</c> (branch-local today when omitted) is on or after it, never in the future and always after the lock
/// date. Per receivable, an event never happens before the row's business date nor before its predecessor on either
/// date, so a cutoff on either basis always counts a prefix of the chain.
/// </summary>
public class SettlementEventDating(IBranchClock branchClock, LockDateGuard lockDateGuard)
{
    public async Task<SettlementEventDates> Resolve(
        Guid branchId,
        DateTime occurredOn,
        DateTime? postedOn,
        CancellationToken ct = default)
    {
        var today = branchClock.LocalBusinessDate(branchClock.UtcNow()).Date;
        var occurred = occurredOn.Date;
        var posted = (postedOn ?? today).Date;

        if (occurred > today)
            throw new OnValidationException([ResourcesErrorMessages.SETTLEMENT_OCCURRED_ON_IN_FUTURE]);
        if (posted > today)
            throw new OnValidationException([ResourcesErrorMessages.SETTLEMENT_POSTED_ON_IN_FUTURE]);
        if (posted < occurred)
            throw new OnValidationException([ResourcesErrorMessages.SETTLEMENT_POSTED_ON_BEFORE_OCCURRED_ON]);

        await lockDateGuard.EnsureNotLocked(branchId, posted, ResourcesErrorMessages.SETTLEMENT_POSTED_ON_LOCKED, ct);
        return new SettlementEventDates(occurred, posted, today);
    }

    public static void EnsureChronology(Transaction receivable, SettlementEvent? previous, SettlementEventDates dates)
    {
        if (dates.OccurredOn < receivable.Date.Date)
            throw new ConflictException(ResourcesErrorMessages.SETTLEMENT_OCCURRED_BEFORE_RECEIVABLE_DATE);

        if (previous is not null &&
            (dates.OccurredOn < previous.OccurredOn.Date || dates.PostedOn < previous.PostedOn.Date))
            throw new ConflictException(ResourcesErrorMessages.SETTLEMENT_CHRONOLOGY_INVALID);
    }
}
