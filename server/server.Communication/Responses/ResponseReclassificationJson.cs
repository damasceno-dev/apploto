using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Posted rows of one type in an open window whose frozen classification differs from the type's current markers
/// (M9.7 Phase 1.3), or the outcome of reclassifying them.
/// </summary>
public class ResponseReclassificationJson
{
    public Guid TransactionTypeId { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }

    /// <summary>The branch lock date; rows on or before it are never candidates.</summary>
    public DateTime? LockDate { get; init; }

    /// <summary>The markers a reclassification applies (the type's current configuration).</summary>
    public SettlementInstrument TargetSettlementInstrument { get; init; }
    public MonthlyFinancialRole TargetMonthlyFinancialRole { get; init; }
    public FinancialClass TargetFinancialClass { get; init; }

    /// <summary>Set on a reclassification outcome: the audit batch shared by its revisions.</summary>
    public Guid? BatchId { get; init; }

    public IReadOnlyList<ResponseReclassificationRowJson> Rows { get; init; } = [];
}

public class ResponseReclassificationRowJson
{
    public Guid TransactionId { get; init; }
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public Direction Direction { get; init; }
    public uint Version { get; init; }
    public ResponseTransactionClassificationJson? Classification { get; init; }
}
