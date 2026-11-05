namespace server.Domain.Entities;

/// <summary>An immutable exact candidate; confirmation never recalculates a replacement candidate.</summary>
public class BankMatchProposal : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid BankAccountId { get; init; }
    public Guid LineId { get; init; }
    public BankStatementLine Line { get; init; } = null!;
    public uint LineVersion { get; init; }
    public Guid ReceiptId { get; init; }
    public BankReceipt Receipt { get; init; } = null!;
    public uint ReceiptVersion { get; init; }
    public string ExpectationFingerprint { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public Guid CreatedByUserId { get; init; }
}

public class BankStatementMatch : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid ProposalId { get; init; }
    public BankMatchProposal Proposal { get; init; } = null!;
    public Guid LineId { get; init; }
    public Guid ReceiptId { get; init; }
    public decimal Amount { get; init; }
    public DateTime PostedOn { get; init; }
    public Guid CreatedByUserId { get; init; }
}
