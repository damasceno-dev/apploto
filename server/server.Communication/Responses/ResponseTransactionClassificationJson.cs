using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>A posted row's frozen classification snapshot (M9.7 Phase 1.3).</summary>
public class ResponseTransactionClassificationJson
{
    public SettlementInstrument SettlementInstrument { get; init; }
    public MonthlyFinancialRole MonthlyFinancialRole { get; init; }
    public FinancialClass FinancialClass { get; init; }
    public ClassificationProvenance Provenance { get; init; }

    /// <summary>1 when posted; each open-period reclassification adds one.</summary>
    public int Revision { get; init; }
    public DateTime ClassifiedAt { get; init; }
}
