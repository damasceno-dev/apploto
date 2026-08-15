using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class DailyClose : EntityBase
{
    public uint Version { get; set; }
    public DateTime Date { get; init; }
    public DailyCloseStatus Status { get; set; } = DailyCloseStatus.Draft;

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public Guid OpenedByUserId { get; init; }
    public User OpenedByUser { get; init; } = null!;

    public Guid? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }

    public Guid? RecordedByOperatorId { get; set; }
    public Operator? RecordedByOperator { get; set; }

    public Guid? SubmittedByUserId { get; set; }
    public User? SubmittedByUser { get; set; }

    public Guid? SubmittedByOperatorId { get; set; }
    public Operator? SubmittedByOperator { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

    public DateTime? ItemsFirstRecordedAt { get; set; }
    public DateTime? OpeningRecheckRequiredAt { get; set; }

    public Guid? OpeningRecheckTriggeredByDailyCloseId { get; set; }
    public DailyClose? OpeningRecheckTriggeredByDailyClose { get; set; }

    public Guid? OpeningRecheckTriggeredByUserId { get; set; }
    public User? OpeningRecheckTriggeredByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public ICollection<DailyCloseItem> Items { get; init; } = [];
}
