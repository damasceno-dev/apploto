namespace server.Domain.Entities;

public class Setting : EntityBase
{
    public DateTime LockDate { get; init; }
    public decimal DailyTargetHours { get; init; } = 7.33m;
    public decimal LunchDeductionOver6H { get; init; } = 1.0m;
    public decimal LunchDeductionOver4H { get; init; } = 0.25m;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
