namespace server.Domain.Models.Projections;

public record OperatorTransactionSummaryProjection(
    int TotalCount,
    decimal TotalIn,
    decimal TotalOut,
    IReadOnlyList<CategoryTotal> ByCategory);
