using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class DailyClose : EntityBase
{
    public DateTime Date { get; init; }
    public DailyCloseStatus Status { get; set; } = DailyCloseStatus.Draft;

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public Guid? SubmittedByOperatorId { get; set; }
    public Operator? SubmittedByOperator { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public ICollection<DailyCloseItem> Items { get; init; } = [];
}
