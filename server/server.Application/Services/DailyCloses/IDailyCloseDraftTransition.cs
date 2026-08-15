using server.Domain.Entities;

namespace server.Application.Services.DailyCloses;

public interface IDailyCloseDraftTransition
{
    void ApplyRecall(DailyClose close, DateTime now, Guid triggeringUserId);
    void ApplyReopen(DailyClose close, DateTime now, Guid triggeringUserId);
    void ApplyRejectedCorrection(DailyClose close, DateTime now, Guid triggeringUserId);
    void ApplyOpeningRecheck(
        DailyClose close,
        DailyClose triggeringClose,
        DateTime now,
        Guid triggeringUserId);
}
