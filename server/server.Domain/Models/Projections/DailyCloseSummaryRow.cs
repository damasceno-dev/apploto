using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

/// <summary>An active close with its account and the recorder/submitter identities (user and operator) it shows.</summary>
public record DailyCloseSummaryRow(
    Guid DailyCloseId,
    DateTime Date,
    DateTime CreatedAt,
    Guid AccountId,
    string AccountName,
    bool AccountActive,
    DailyCloseStatus Status,
    Guid? RecordedByUserId,
    string? RecordedByUserName,
    Guid? RecordedByOperatorId,
    string? RecordedByOperatorName,
    Guid? SubmittedByUserId,
    string? SubmittedByUserName,
    Guid? SubmittedByOperatorId,
    string? SubmittedByOperatorName);

/// <summary>Active close count per status over a window.</summary>
public record DailyCloseStatusCountRow(DailyCloseStatus Status, int Count);
