namespace server.Domain.Models.Projections;

/// <summary>
/// An operator's summary totals; the <c>Generated*</c> members are the automatic share already included in the
/// totals (M7.7 Phase 5.8).
/// </summary>
public record OperatorTransactionSummaryProjection(
    int TotalCount,
    decimal TotalIn,
    decimal TotalOut,
    IReadOnlyList<CategoryTotal> ByCategory,
    int GeneratedCount = 0,
    decimal GeneratedIn = 0m,
    decimal GeneratedOut = 0m);
