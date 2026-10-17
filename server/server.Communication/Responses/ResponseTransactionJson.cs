using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseTransactionJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public string? Description { get; init; }
    public TimeOnly? TransactionTime { get; init; }
    public Guid TransactionTypeId { get; init; }
    public Guid CategoryId { get; init; }
    public Direction Direction { get; init; }
    public Guid AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? PaidAt { get; init; }
    public Guid? OriginTransactionId { get; init; }

    /// <summary>
    /// System-owned generation origin (M7.7 Phase 5.7): non-null only on a generated Terminal leg, with
    /// <c>GeneratedFromTransactionId</c> naming its Tab source. Never accepted from a request.
    /// </summary>
    public TransactionGenerationKind? GenerationKind { get; init; }
    public Guid? GeneratedFromTransactionId { get; init; }

    /// <summary>On a fiado source with a physical leg: the generated Terminal row's id (M7.7 Phase 5.6).</summary>
    public Guid? GeneratedTransactionId { get; init; }
    public Guid RecordedByOperatorId { get; init; }
    public Guid CreatedByUserId { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? UpdatedByUserId { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTime? CancelledAt { get; init; }
    public Guid? CancelledByUserId { get; init; }
    public string? CancellationReason { get; init; }
    public Guid BranchId { get; init; }
    public DateTime CreatedAt { get; init; }
}
