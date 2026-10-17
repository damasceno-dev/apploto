using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class DailyCloseReviewEventsRepository(ServerDbContext dbContext) : IDailyCloseReviewEventsRepository
{
    public async Task Add(DailyCloseReviewEvent reviewEvent, CancellationToken ct = default)
    {
        await dbContext.DailyCloseReviewEvents.AddAsync(reviewEvent, ct);
    }

    public async Task<IReadOnlyList<DailyCloseReviewEvent>> ListByDailyCloseIdAndBranchIdAsNoTracking(
        Guid dailyCloseId,
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.DailyCloseReviewEvents
            .AsNoTracking()
            .Include(reviewEvent => reviewEvent.Items)
            .Include(reviewEvent => reviewEvent.SourceDailyClose)
            .Include(reviewEvent => reviewEvent.PreviousSourceDailyClose)
            .Where(reviewEvent =>
                reviewEvent.DailyCloseId == dailyCloseId &&
                reviewEvent.BranchId == branchId &&
                reviewEvent.Active)
            .OrderBy(reviewEvent => reviewEvent.OccurredAt)
            .ThenBy(reviewEvent => reviewEvent.Id)
            .ToListAsync(ct);
    }
}
