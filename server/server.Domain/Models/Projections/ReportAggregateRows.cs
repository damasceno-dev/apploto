using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

/// <summary>Official variance rows of one close status folded in the database: count, sum, max and min.</summary>
public record OfficialVarianceStatusTotalRow(DailyCloseStatus Status, int Count, decimal Total, decimal Max, decimal Min);

/// <summary>The fiado balance split over the shared netting projection, computed in the database.</summary>
public record FiadoBalanceSummary(int DebtorCount, decimal TotalDebt, int CreditorCount, decimal TotalCredit);

/// <summary>Inflow and outflow sums and the row count of one monthly financial role over a window.</summary>
public record MonthlyFinancialRoleTotal(decimal InTotal, decimal OutTotal, int RowCount);

/// <summary>A bounded page of listed accounts or operators with the size of the whole listed set.</summary>
public record ActivityPage<TRow>(int TotalCount, IReadOnlyList<TRow> Items);
