namespace server.Communication.Requests;

public class RequestBankStatementImportJson
{
    public Guid AccountBindingId { get; init; }
    public Guid BankAccountId { get; init; }
    public string BankIdentity { get; init; } = string.Empty;
    public string AccountIdentity { get; init; } = string.Empty;
    public string ContentBase64 { get; init; } = string.Empty;
}

public class RequestBankMatchProposeJson
{
    public Guid ImportId { get; init; }
}

public class RequestBankMatchConfirmJson
{
    public DateTime PostedOn { get; init; }
    public IReadOnlyList<RequestBankMatchItemJson> Items { get; init; } = [];
}

public class RequestBankMatchItemJson
{
    public Guid ProposalId { get; init; }
    public Guid LineId { get; init; }
    public uint LineVersion { get; init; }
    public Guid ReceiptId { get; init; }
    public uint ReceiptVersion { get; init; }
    public decimal Amount { get; init; }
    public string ExpectationFingerprint { get; init; } = string.Empty;
}
