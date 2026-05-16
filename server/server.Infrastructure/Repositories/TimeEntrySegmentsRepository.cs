using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class TimeEntrySegmentsRepository(ServerDbContext dbContext) : ITimeEntrySegmentsRepository
{
    public async Task Add(TimeEntrySegment segment)
    {
        await dbContext.TimeEntrySegments.AddAsync(segment);
    }

    public async Task<TimeEntrySegment?> GetActiveByIdAndBranchId(Guid segmentId, Guid branchId)
    {
        return await dbContext.TimeEntrySegments
            .Include(s => s.TimeEntry)
                .ThenInclude(te => te.Operator)
            .Include(s => s.TimeEntry)
                .ThenInclude(te => te.Segments
                    .Where(seg => seg.Active)
                    .OrderBy(seg => seg.ClockIn)
                    .ThenBy(seg => seg.CreatedAt)
                    .ThenBy(seg => seg.Id))
            .FirstOrDefaultAsync(s =>
                s.Id == segmentId &&
                s.TimeEntry.BranchId == branchId &&
                s.Active);
    }

    public async Task<TimeEntrySegment?> GetActiveByIdAndBranchIdAsNoTracking(Guid segmentId, Guid branchId)
    {
        return await dbContext.TimeEntrySegments
            .AsNoTracking()
            .Include(s => s.TimeEntry)
                .ThenInclude(te => te.Operator)
            .Include(s => s.TimeEntry)
                .ThenInclude(te => te.Segments
                    .Where(seg => seg.Active)
                    .OrderBy(seg => seg.ClockIn)
                    .ThenBy(seg => seg.CreatedAt)
                    .ThenBy(seg => seg.Id))
            .FirstOrDefaultAsync(s =>
                s.Id == segmentId &&
                s.TimeEntry.BranchId == branchId &&
                s.Active);
    }
}
