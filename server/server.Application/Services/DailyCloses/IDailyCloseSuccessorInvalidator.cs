using server.Domain.Entities;

namespace server.Application.Services.DailyCloses;

public interface IDailyCloseSuccessorInvalidator
{
    Task<DailyCloseSuccessorInvalidation?> InvalidateNextEligible(
        DailyClose triggeringClose,
        DateTime now,
        Guid triggeringUserId,
        CancellationToken ct = default);
}
