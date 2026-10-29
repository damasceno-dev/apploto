using server.Domain.Entities;

namespace server.Application.Services.DailyCloses;

public interface IDailyCloseSuccessorInvalidator
{
    Task<DailyCloseSuccessorInvalidation?> InvalidateNextEligible(
        DailyClose triggeringClose,
        IReadOnlyDictionary<Guid, decimal> newClosingValues,
        DateTime now,
        User triggeringUser,
        CancellationToken ct = default);
}
