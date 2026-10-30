namespace server.Domain.Entities;

/// <summary>
/// The physical CAIXA terminal: the reference for its report. It holds no cash, Tab or ledger balance; custody
/// lives on the cashbox <see cref="Account"/> that points at it.
/// </summary>
public class Terminal : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
