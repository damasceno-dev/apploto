namespace server.Domain.Entities;

/// <summary>Explicit immutable OFX identity for one ledger bank account; display Institution/Number are not source ids.</summary>
public class BankStatementAccountBinding : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid BankAccountId { get; init; }
    public uint Version { get; set; }
    public string BankIdentity { get; init; } = string.Empty;
    public string AccountIdentity { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
}
