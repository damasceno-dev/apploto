namespace server.Communication.Requests;

public class RequestUpdateSettingJson
{
    public bool ClearPersonalShortageThreshold { get; init; }
    public DateTime? LockDate { get; init; }
    public decimal? PersonalShortageThreshold { get; init; }

    public decimal? DailyTargetHours { get; init; }
    public decimal? LunchDeductionOver6H { get; init; }
    public decimal? LunchDeductionOver4H { get; init; }
}
