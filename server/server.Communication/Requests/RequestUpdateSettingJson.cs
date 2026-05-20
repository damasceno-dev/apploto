namespace server.Communication.Requests;

public class RequestUpdateSettingJson
{
    public DateTime? LockDate { get; init; }
    public decimal? DailyTargetHours { get; init; }
    public decimal? LunchDeductionOver6H { get; init; }
    public decimal? LunchDeductionOver4H { get; init; }
}
