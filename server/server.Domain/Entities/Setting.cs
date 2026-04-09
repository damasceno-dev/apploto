namespace server.Domain.Entities;

public class Setting : EntityBase
{
    public DateTime LockDate { get; set; }
    public decimal DailyTargetHours { get; set; } = 7.33m;
    public decimal LunchDeductionOver6H { get; set; } = 1.0m;
    public decimal LunchDeductionOver4H { get; set; } = 0.25m;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
