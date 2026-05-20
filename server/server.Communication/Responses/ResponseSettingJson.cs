namespace server.Communication.Responses;

public class ResponseSettingJson
{
    public Guid Id { get; init; }
    public DateTime LockDate { get; init; }
    public decimal DailyTargetHours { get; init; }
    public decimal LunchDeductionOver6H { get; init; }
    public decimal LunchDeductionOver4H { get; init; }
    public Guid BranchId { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool Active { get; init; }
}
