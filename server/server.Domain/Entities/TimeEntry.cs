using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class TimeEntry : EntityBase
{
    public DateTime Date { get; init; }
    public TimeEntryStatus Status { get; set; }
    public decimal TotalHours { get; set; }
    public decimal BalanceHours { get; set; }

    public Guid OperatorId { get; init; }
    public Operator Operator { get; init; } = null!;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<TimeEntrySegment> Segments { get; init; } = [];
}
