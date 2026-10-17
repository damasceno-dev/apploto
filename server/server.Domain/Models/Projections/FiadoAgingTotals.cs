namespace server.Domain.Models.Projections;

/// <summary>Whole-set FIFO aging totals: remainders grouped by due date and the oldest unpaid sale date.</summary>
public record FiadoAgingTotals(IReadOnlyList<AgingDueDateTotalRow> ByDueDate, DateTime? OldestUnpaidDate);
