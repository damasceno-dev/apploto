using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

public record DashboardCloseRow(
    Guid DailyCloseId,
    Guid AccountId,
    string AccountName,
    Guid? SubmittedByOperatorId,
    string? SubmittedByOperatorName,
    DailyCloseStatus Status,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt);
