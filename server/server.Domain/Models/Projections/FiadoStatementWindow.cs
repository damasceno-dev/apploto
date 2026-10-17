namespace server.Domain.Models.Projections;

/// <summary>
/// The statement window's boundary balances and size. <see cref="OpeningBalance"/> is the signed balance of every
/// movement dated before the window; <see cref="ClosingBalance"/> adds every movement inside it, across all pages.
/// </summary>
public record FiadoStatementWindow(decimal OpeningBalance, decimal ClosingBalance, int TotalCount);
