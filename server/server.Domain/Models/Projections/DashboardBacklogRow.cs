namespace server.Domain.Models.Projections;

public record DashboardBacklogRow(int SubmittedCount, int RejectedCount, int DraftCount, DateTime? OldestPendingDate);
