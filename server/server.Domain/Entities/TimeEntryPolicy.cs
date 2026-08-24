namespace server.Domain.Entities;

/// <summary>
/// Effective-dated time-entry policy row (M7.7 Phase 7, decision 1.6a). Each row carries
/// the branch's time constants valid from <see cref="EffectiveFrom"/> (inclusive) until a
/// later row's <see cref="EffectiveFrom"/> supersedes it. Every calculation resolves the
/// row applicable to the entry's date, so changing today's constants never rewrites
/// historical balances. The branch's unique <c>Setting</c> row keeps mirroring the latest
/// values for the settings read surface but is never a calculation input.
/// </summary>
public class TimeEntryPolicy : EntityBase
{
    public DateTime EffectiveFrom { get; init; }
    public decimal DailyTargetHours { get; set; }
    public decimal LunchDeductionOver6H { get; set; }
    public decimal LunchDeductionOver4H { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
