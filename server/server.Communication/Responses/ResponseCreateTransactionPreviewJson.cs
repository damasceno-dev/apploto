namespace server.Communication.Responses;

/// <summary>
/// Response for the create-impact preview (<c>POST /transaction/preview</c>). Unlike the
/// edit-impact preview there is no <c>TransactionId</c> — nothing is created. Carries the shared
/// <see cref="ResponseTransactionImpactJson"/> envelope plus a currently-always-empty
/// <see cref="Warnings"/> list.
/// </summary>
public class ResponseCreateTransactionPreviewJson
{
    public ResponseTransactionImpactJson Impact { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
