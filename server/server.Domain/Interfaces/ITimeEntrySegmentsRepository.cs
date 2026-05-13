using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ITimeEntrySegmentsRepository
{
    Task Add(TimeEntrySegment segment);
    Task<TimeEntrySegment?> GetActiveByIdAndBranchId(Guid segmentId, Guid branchId);
    Task<TimeEntrySegment?> GetActiveByIdAndBranchIdAsNoTracking(Guid segmentId, Guid branchId);
}
