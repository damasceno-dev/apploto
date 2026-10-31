using server.Domain.Entities;
using server.Exceptions;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.Sessions;

/// <summary>
/// Separation of duties (§6.20 ApproveSession, M9.6 Phase 5): the approver is neither the session's recorder, its current
/// submitter nor its custodian. Identity is compared by user and by operator, and the custodian also by the user captured
/// at start and the operator's current login link, so relinking a login cannot bypass the exclusion.
/// </summary>
public static class SessionApprovalPolicy
{
    public const int ReasonMaxLength = 500;

    /// <summary>The verbatim reason ordinary approval is refused to this approver, or null when they are independent.</summary>
    public static string? DutyConflict(DailyClose session, Guid approverUserId, Operator? approverOperator)
    {
        var approverOperatorId = approverOperator?.Id;

        if (session.RecordedByUserId == approverUserId
            || (approverOperatorId is not null && session.RecordedByOperatorId == approverOperatorId))
            return ResourcesErrorMessages.SESSION_APPROVER_IS_RECORDER;

        if (session.SubmittedByUserId == approverUserId
            || (approverOperatorId is not null && session.SubmittedByOperatorId == approverOperatorId))
            return ResourcesErrorMessages.SESSION_APPROVER_IS_SUBMITTER;

        if (session.CustodianUserIdAtStart == approverUserId
            || session.CustodianOperator?.UserId == approverUserId
            || (approverOperatorId is not null && session.CustodianOperatorId == approverOperatorId))
            return ResourcesErrorMessages.SESSION_APPROVER_IS_CUSTODIAN;

        return null;
    }
}
