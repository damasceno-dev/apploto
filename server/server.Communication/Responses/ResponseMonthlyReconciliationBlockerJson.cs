using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// One reason the month is not lock-ready. The backend returns structured data — not a formatted
/// sentence — so web and mobile can render (and localize) the message themselves and group/sort blockers.
/// </summary>
public class ResponseMonthlyReconciliationBlockerJson
{
    /// <summary>Which kind of blocker this is, so clients format and group without parsing text.</summary>
    public MonthlyReconciliationBlockerType Type { get; init; }

    /// <summary>Day of month (1–31) the blocker falls on.</summary>
    public int Day { get; init; }

    /// <summary>Id of the offending close, so clients can deep-link to it. Set only for <c>UnapprovedClose</c>.</summary>
    public Guid? DailyCloseId { get; init; }

    /// <summary>Expected account id. Set for <c>UnapprovedClose</c> and <c>MissingExpectedClose</c>.</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Expected account name. Set for <c>UnapprovedClose</c> and <c>MissingExpectedClose</c>.</summary>
    public string? AccountName { get; init; }

    /// <summary>The non-Approved close status (Draft/Submitted/Rejected). Set only when <see cref="Type"/> is <c>UnapprovedClose</c>.</summary>
    public DailyCloseStatus? CloseStatus { get; init; }

    /// <summary>Number of outstanding Draft transactions on the day. Set only when <see cref="Type"/> is <c>DraftTransactions</c>.</summary>
    public int? DraftTransactionCount { get; init; }
}
