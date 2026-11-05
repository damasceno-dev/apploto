namespace server.Domain.Entities;

public class BankStatementImport : EntityBase
{
    public Guid? AccountBindingId { get; init; }
    public BankStatementAccountBinding? AccountBinding { get; init; }
    public Guid BranchId { get; init; }
    public Guid BankAccountId { get; init; }
    public string Fingerprint { get; init; } = string.Empty;
    public byte[] Original { get; init; } = [];
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public Guid CreatedByUserId { get; init; }
    public ICollection<BankStatementImportLine> Lines { get; init; } = [];
}

public class BankStatementImportLine
{
    public Guid ImportId { get; init; }
    public BankStatementImport Import { get; init; } = null!;
    public Guid LineId { get; init; }
    public BankStatementLine Line { get; init; } = null!;
}
