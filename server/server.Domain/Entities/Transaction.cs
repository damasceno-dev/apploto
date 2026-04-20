using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class Transaction : EntityBase
{
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public string? Description { get; set; }
    public TimeOnly? TransactionTime { get; set; }

    public Guid TransactionTypeId { get; init; }
    public TransactionType TransactionType { get; init; } = null!;

    public Guid CategoryId { get; init; }
    public Category Category { get; init; } = null!;

    public Direction Direction { get; init; }

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public Guid? OriginTransactionId { get; init; }
    public Transaction? OriginTransaction { get; set; }

    public Guid RecordedByOperatorId { get; init; }
    public Operator RecordedByOperator { get; init; } = null!;

    public Guid CreatedByUserId { get; init; }
    public User CreatedByUser { get; init; } = null!;

    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancellationReason { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
