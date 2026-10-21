using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseTransactionTypeJson
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SettlementRule SettlementRule { get; set; }
    public bool RequiresTabAccountAndClient { get; set; }

    /// <summary>Non-null on the two system-owned counterpart types: filterable, never offered for manual create.</summary>
    public TransactionTypeSystemRole? SystemRole { get; set; }

    /// <summary>Non-null when the type is a fiado repayment method usable by <i>Receber pagamento</i>.</summary>
    public FiadoPaymentKind? FiadoPaymentKind { get; set; }

    /// <summary>Receivable classification (M11 Phase 4); <c>None</c> until configured.</summary>
    public SettlementInstrument SettlementInstrument { get; set; }

    /// <summary>Monthly financial report classification (M11 Phase 6); <c>None</c> until configured.</summary>
    public MonthlyFinancialRole MonthlyFinancialRole { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Active { get; set; }
}
