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

    public Guid? TabAccountId { get; set; }
    public Account? TabAccount { get; set; }

    // Physical CAIXA terminal of a cashbox (M9.6 Phase 2); null on Tab/Bank accounts.
    public Guid? TerminalId { get; set; }
    public Terminal? Terminal { get; set; }

    // Server-only serializer of custody-interval writes, incremented by the session interval trigger.
    public long CustodyHistoryRevision { get; set; }

    public ICollection<OperatorAccount> OperatorAccounts { get; init; } = [];
}
