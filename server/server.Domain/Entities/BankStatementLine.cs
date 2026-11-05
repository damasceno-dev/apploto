namespace server.Domain.Entities;

public class BankStatementLine : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid BankAccountId { get; init; }
    public uint Version { get; set; }
    public string? BankIdentity { get; init; }
    public string Fingerprint { get; init; } = string.Empty;
    public DateTime PostedOn { get; init; }
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Memo { get; init; }
    public string Original { get; init; } = string.Empty;
    public bool AmbiguousIdentity { get; init; }
}
