using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestBankMovementJson
{
    public BankMovementKind Kind { get; init; }
    public Guid BankAccountId { get; init; }
    public Guid? SafeAccountId { get; init; }
    public Guid BankTransactionTypeId { get; init; }
    public Guid? SourceTransactionTypeId { get; init; }
    public Guid RecordedByOperatorId { get; init; }
    public decimal Amount { get; init; }
    public DateTime OccurredOn { get; init; }
    public DateTime? PostedOn { get; init; }
    public bool ObservedBankMovement { get; init; }
    public string Evidence { get; init; } = string.Empty;
}
