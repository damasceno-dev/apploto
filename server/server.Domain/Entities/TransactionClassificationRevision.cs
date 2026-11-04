using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// Audit of one explicit open-period reclassification of one posted row (M9.7 Phase 1.3): the snapshot before and
/// after, the reason and the batch it belongs to. The database refuses a snapshot change without the matching
/// revision; revisions are immutable.
/// </summary>
public class TransactionClassificationRevision : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid BatchId { get; init; }
    public int Revision { get; init; }

    public SettlementInstrument? PreviousSettlementInstrument { get; init; }
    public MonthlyFinancialRole? PreviousMonthlyFinancialRole { get; init; }
    public FinancialClass? PreviousFinancialClass { get; init; }
    public ClassificationProvenance? PreviousProvenance { get; init; }

    public SettlementInstrument SettlementInstrument { get; init; }
    public MonthlyFinancialRole MonthlyFinancialRole { get; init; }
    public FinancialClass FinancialClass { get; init; }

    public string Reason { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
}
