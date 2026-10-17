using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class Transaction : EntityBase
{
    public uint Version { get; set; }
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

    // System-owned generation origin (M7.7 Phase 5.7): set only by the server when it writes the Terminal leg of a
    // physical fiado operation; both are null on every ordinary row and never bound from a request.
    public TransactionGenerationKind? GenerationKind { get; init; }
    public Guid? GeneratedFromTransactionId { get; init; }
    public Transaction? GeneratedFromTransaction { get; set; }

    public Guid RecordedByOperatorId { get; init; }
    public Operator RecordedByOperator { get; init; } = null!;

    public Guid CreatedByUserId { get; init; }
    public User CreatedByUser { get; init; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancellationReason { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
