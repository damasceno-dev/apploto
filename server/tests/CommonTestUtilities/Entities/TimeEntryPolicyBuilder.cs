using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class TimeEntryPolicyBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _branchId = Guid.NewGuid();
    private DateTime _effectiveFrom = DateTime.MinValue;
    private decimal _dailyTargetHours = 7.33m;
    private decimal _lunchDeductionOver6H = 1.0m;
    private decimal _lunchDeductionOver4H = 0.25m;
    private DateTime _createdAt = DateTime.UtcNow;

    public TimeEntryPolicyBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TimeEntryPolicyBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public TimeEntryPolicyBuilder WithEffectiveFrom(DateTime effectiveFrom)
    {
        _effectiveFrom = effectiveFrom;
        return this;
    }

    public TimeEntryPolicyBuilder WithDailyTargetHours(decimal dailyTargetHours)
    {
        _dailyTargetHours = dailyTargetHours;
        return this;
    }

    public TimeEntryPolicyBuilder WithLunchDeductionOver6H(decimal lunchDeductionOver6H)
    {
        _lunchDeductionOver6H = lunchDeductionOver6H;
        return this;
    }

    public TimeEntryPolicyBuilder WithLunchDeductionOver4H(decimal lunchDeductionOver4H)
    {
        _lunchDeductionOver4H = lunchDeductionOver4H;
        return this;
    }

    public TimeEntryPolicyBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public TimeEntryPolicy Build()
    {
        return new TimeEntryPolicy
        {
            Id = _id,
            BranchId = _branchId,
            EffectiveFrom = _effectiveFrom,
            DailyTargetHours = _dailyTargetHours,
            LunchDeductionOver6H = _lunchDeductionOver6H,
            LunchDeductionOver4H = _lunchDeductionOver4H,
            CreatedAt = _createdAt
        };
    }
}
