using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseBankMovementJson
{
    public Guid Id { get; init; }
    public BankMovementKind Kind { get; init; }
    public Guid BankTransactionId { get; init; }
    public Guid? SourceTransactionId { get; init; }
    public ResponseBankReceiptJson BankReceipt { get; init; } = null!;
}
