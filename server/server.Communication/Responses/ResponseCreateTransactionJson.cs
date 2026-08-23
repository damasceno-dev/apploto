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
    public DateTime? PaidAt { get; set; }
    public Guid RecordedByOperatorId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public TransactionStatus Status { get; set; }
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
}
