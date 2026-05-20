using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class SettingBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _branchId = Guid.NewGuid();
    private DateTime _lockDate = DateTime.MinValue;
    private decimal _dailyTargetHours = 7.33m;
    private decimal _lunchDeductionOver6H = 1.0m;
    private decimal _lunchDeductionOver4H = 0.25m;

    public SettingBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public SettingBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public SettingBuilder WithLockDate(DateTime lockDate)
    {
        _lockDate = lockDate;
        return this;
    }

    public SettingBuilder WithDailyTargetHours(decimal dailyTargetHours)
    {
        _dailyTargetHours = dailyTargetHours;
        return this;
    }

    public SettingBuilder WithLunchDeductionOver6H(decimal lunchDeductionOver6H)
    {
        _lunchDeductionOver6H = lunchDeductionOver6H;
        return this;
    }

    public SettingBuilder WithLunchDeductionOver4H(decimal lunchDeductionOver4H)
    {
        _lunchDeductionOver4H = lunchDeductionOver4H;
        return this;
    }

    public Setting Build()
    {
        return new Setting
        {
            Id = _id,
            BranchId = _branchId,
            LockDate = _lockDate,
            DailyTargetHours = _dailyTargetHours,
            LunchDeductionOver6H = _lunchDeductionOver6H,
            LunchDeductionOver4H = _lunchDeductionOver4H
        };
    }
}
