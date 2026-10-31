using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class TransactionType : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public SettlementRule SettlementRule { get; set; }
    public bool RequiresTabAccountAndClient { get; set; }

    /// <summary>Non-null for a system-owned type; such a type is never used by a generic manual write.</summary>
    public TransactionTypeSystemRole? SystemRole { get; init; }

    /// <summary>
    /// Server-only identity of a seeded row (M9.6 Phase 9), e.g. <c>expense.agua</c>. Seeding finds its own rows by
    /// this key and never adopts a same-name custom type; null on every type an admin created.
    /// </summary>
    public string? SeedKey { get; init; }

    /// <summary>
    /// Non-null marks a fiado repayment method usable by <i>Receber pagamento</i>; the value decides whether the
    /// payment moves counted cash. Only valid on an <c>In</c> type that requires a Tab account and a client.
    /// </summary>
    public FiadoPaymentKind? FiadoPaymentKind { get; set; }

    /// <summary>
    /// Receivable classification (M11 Phase 4): report metadata read at query time, so changing it reclassifies the
    /// type's rows in current-state receivable reports without touching any row, balance or variance.
    /// </summary>
    public SettlementInstrument SettlementInstrument { get; set; } = SettlementInstrument.None;

    /// <summary>Monthly financial report classification (M11 Phase 6), read at query time like the instrument.</summary>
    public MonthlyFinancialRole MonthlyFinancialRole { get; set; } = MonthlyFinancialRole.None;

    public Guid CategoryId { get; init; }
    public Category Category { get; init; } = null!;
}
