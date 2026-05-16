using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class TimeEntrySegmentsRepositoryBuilder
{
    private readonly ITimeEntrySegmentsRepository _repository = Substitute.For<ITimeEntrySegmentsRepository>();

    public TimeEntrySegmentsRepositoryBuilder GetActiveByIdAndBranchIdReturns(
        Guid segmentId,
        Guid branchId,
        TimeEntrySegment? result)
    {
        _repository.GetActiveByIdAndBranchId(
                Arg.Is<Guid>(value => value == segmentId),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public TimeEntrySegmentsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTrackingReturns(
        Guid segmentId,
        Guid branchId,
        TimeEntrySegment? result)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == segmentId),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public ITimeEntrySegmentsRepository Build()
    {
        return _repository;
    }
}
