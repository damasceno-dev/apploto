namespace server.Communication.Responses;

/// <summary>
/// The outcome of a cheque command (M9.7 Phase 2): every affected receivable as of branch-local today on the posted
/// basis, the pending bank receipt a deposit or return touched, and the fiado debt a conversion opened. A replay with the
/// same <c>Idempotency-Key</c> returns this exact envelope.
/// </summary>
public class ResponseChequeOperationJson
{
    public IReadOnlyList<ResponseReceivableJson> Receivables { get; init; } = [];
    public ResponseBankReceiptJson? BankReceipt { get; init; }
    public ResponseBankReceiptJson? ReturnBankReceipt { get; init; }
    public Guid? ConvertedTransactionId { get; init; }
}
