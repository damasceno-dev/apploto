using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateSettingJsonBuilder
{
    private DateTime? _lockDate = null;
    private decimal? _dailyTargetHours = null;
    private decimal? _lunchDeductionOver6H = null;
    private decimal? _lunchDeductionOver4H = null;

    public RequestUpdateSettingJsonBuilder WithLockDate(DateTime lockDate)
    {
        _lockDate = lockDate;
        return this;
    }

    public RequestUpdateSettingJsonBuilder WithDailyTargetHours(decimal dailyTargetHours)
    {
        _dailyTargetHours = dailyTargetHours;
        return this;
    }

    public RequestUpdateSettingJsonBuilder WithLunchDeductionOver6H(decimal lunchDeductionOver6H)
    {
        _lunchDeductionOver6H = lunchDeductionOver6H;
        return this;
    }

    public RequestUpdateSettingJsonBuilder WithLunchDeductionOver4H(decimal lunchDeductionOver4H)
    {
        _lunchDeductionOver4H = lunchDeductionOver4H;
        return this;
    }

    public RequestUpdateSettingJson Build()
    {
        return new RequestUpdateSettingJson
        {
            LockDate = _lockDate,
            DailyTargetHours = _dailyTargetHours,
            LunchDeductionOver6H = _lunchDeductionOver6H,
            LunchDeductionOver4H = _lunchDeductionOver4H
        };
    }
}
