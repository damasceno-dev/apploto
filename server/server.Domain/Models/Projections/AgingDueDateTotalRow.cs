namespace server.Domain.Models.Projections;

/// <summary>Open value and row count grouped by due date; the use case buckets each date at its as-of date.</summary>
public record AgingDueDateTotalRow(DateTime DueDate, decimal Value, int RowCount);
