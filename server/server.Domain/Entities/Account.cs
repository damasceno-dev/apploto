using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class Account : EntityBase
{
    public AccountType Type { get; init; }

    public string Name { get; set; } = string.Empty;

    public string? Institution { get; set; }

    public string? Number { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public Guid? TabAccountId { get; init; }
    public Account? TabAccount { get; init; }

    public ICollection<OperatorAccount> OperatorAccounts { get; init; } = [];
}
