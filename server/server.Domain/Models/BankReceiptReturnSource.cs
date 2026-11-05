namespace server.Domain.Models;

public sealed record BankReceiptReturnSource(Guid ReceivableTransactionId, Guid? OriginalBankReceiptId);
