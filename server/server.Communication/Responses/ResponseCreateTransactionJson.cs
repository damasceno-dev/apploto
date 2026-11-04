using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCreateTransactionJson
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string? Description { get; set; }
    public TimeOnly? TransactionTime { get; set; }
    public Guid TransactionTypeId { get; set; }
    public Guid CategoryId { get; set; }
    public Direction Direction { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ClientId { get; set; }
    public DateTime DueDate { get; set; }

    /// <summary>System-owned generation origin; see <c>ResponseTransactionJson</c> (M7.7 Phase 5.7).</summary>
    public TransactionGenerationKind? GenerationKind { get; set; }
    public Guid? GeneratedFromTransactionId { get; set; }

    /// <summary>
    /// When the create also wrote a generated leg — the cashbox leg of a fiado sale or cash repayment (M7.7 Phase 5.6,
    /// M9.6 Phase 6) or the cofre leg of a deposit (M9.6 Phase 9): its id, part of the same commit and replayed with it.
    /// </summary>
    public Guid? GeneratedTransactionId { get; set; }

    /// <summary>The cashbox → cofre transfer this deposit created (M9.6 Phase 9); null on every other create.</summary>
    public Guid? CustodyTransferId { get; set; }
    /// <summary>The cashbox session of this row; null on Tab/Bank rows (M9.6 Phase 2).</summary>
    public Guid? SessionId { get; set; }

    /// <summary>The session this command started through its opening acknowledgment, replayed with it.</summary>
    public Guid? StartedSessionId { get; set; }
    public Guid? OpeningDiscrepancyId { get; set; }

    public Guid RecordedByOperatorId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public TransactionStatus Status { get; set; }
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
}
