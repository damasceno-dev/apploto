namespace server.Domain.Models.Projections;

public record ManagerQueueBacklogRow(int SubmittedCount, int RejectedCount, int DraftCount, DateTime? OldestPendingDate);
