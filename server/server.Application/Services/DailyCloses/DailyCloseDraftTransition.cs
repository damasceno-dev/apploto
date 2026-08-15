using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.Services.DailyCloses;

public sealed class DailyCloseDraftTransition : IDailyCloseDraftTransition
{
    public void ApplyRecall(DailyClose close, DateTime now, Guid triggeringUserId)
    {
        close.Status = DailyCloseStatus.Draft;
        ClearSubmission(close);
        close.RejectionReason = null;
        ClearApproval(close);
        ClearOpeningRecheck(close);
        StampAudit(close, now, triggeringUserId);
    }

    public void ApplyReopen(DailyClose close, DateTime now, Guid triggeringUserId)
    {
        close.Status = DailyCloseStatus.Draft;
        ClearSubmission(close);
        close.RejectionReason = null;
        ClearApproval(close);
        ClearOpeningRecheck(close);
        StampAudit(close, now, triggeringUserId);
    }

    public void ApplyRejectedCorrection(DailyClose close, DateTime now, Guid triggeringUserId)
    {
        close.Status = DailyCloseStatus.Draft;
        ClearSubmission(close);
        ClearApproval(close);
        StampAudit(close, now, triggeringUserId);
    }

    public void ApplyOpeningRecheck(DailyClose close,DailyClose triggeringClose,DateTime now,Guid triggeringUserId)
    {
        close.Status = DailyCloseStatus.Draft;
        ClearSubmission(close);
        ClearApproval(close);
        close.OpeningRecheckRequiredAt = now;
        close.OpeningRecheckTriggeredByDailyCloseId = triggeringClose.Id;
        close.OpeningRecheckTriggeredByDailyClose = triggeringClose;
        close.OpeningRecheckTriggeredByUserId = triggeringUserId;
        StampAudit(close, now, triggeringUserId);
    }

    private static void ClearApproval(DailyClose close)
    {
        close.ApprovedAt = null;
        close.ApprovedByUserId = null;
        close.ApprovedByUser = null;
    }

    private static void ClearSubmission(DailyClose close)
    {
        close.SubmittedAt = null;
        close.SubmittedByUserId = null;
        close.SubmittedByUser = null;
        close.SubmittedByOperatorId = null;
        close.SubmittedByOperator = null;
    }

    private static void ClearOpeningRecheck(DailyClose close)
    {
        close.OpeningRecheckRequiredAt = null;
        close.OpeningRecheckTriggeredByDailyCloseId = null;
        close.OpeningRecheckTriggeredByDailyClose = null;
        close.OpeningRecheckTriggeredByUserId = null;
    }

    private static void StampAudit(DailyClose close, DateTime now, Guid triggeringUserId)
    {
        close.UpdatedAt = now;
        close.UpdatedByUserId = triggeringUserId;
    }
}
