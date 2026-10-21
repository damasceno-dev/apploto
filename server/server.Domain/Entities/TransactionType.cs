using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class TransactionType : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public SettlementRule SettlementRule { get; set; }
    public bool RequiresTabAccountAndClient { get; set; }

    /// <summary>Non-null for the two system-owned counterpart types; such a type is never used for a manual write.</summary>
    public TransactionTypeSystemRole? SystemRole { get; init; }

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
