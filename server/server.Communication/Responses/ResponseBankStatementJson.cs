namespace server.Communication.Responses;

public class ResponseBankStatementJson
{
    public Guid Id { get; init; }
    public Guid? AccountBindingId { get; init; }
    public Guid BankAccountId { get; init; }
    public bool DuplicateImport { get; init; }
    public IReadOnlyList<ResponseBankStatementLineJson> Lines { get; init; } = [];
}

public class ResponseBankStatementLineJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public string? BankIdentity { get; init; }
    public DateTime PostedOn { get; init; }
    public decimal Amount { get; init; }
    public string? Name { get; init; }
    public string? Memo { get; init; }
    public bool AmbiguousIdentity { get; init; }
    public bool Matched { get; init; }
}

public class ResponseBankMatchProposalsJson
{
    public Guid ImportId { get; init; }
    public IReadOnlyList<ResponseBankMatchProposalJson> Proposals { get; init; } = [];
    public IReadOnlyList<Guid> UnmatchedLineIds { get; init; } = [];
}

public class ResponseBankMatchProposalJson
{
    public Guid Id { get; init; }
    public Guid LineId { get; init; }
    public uint LineVersion { get; init; }
    public Guid ReceiptId { get; init; }
    public uint ReceiptVersion { get; init; }
    public decimal Amount { get; init; }
    public string ExpectationFingerprint { get; init; } = string.Empty;
}

public class ResponseBankMatchConfirmationJson
{
    public IReadOnlyList<Guid> MatchIds { get; init; } = [];
}
