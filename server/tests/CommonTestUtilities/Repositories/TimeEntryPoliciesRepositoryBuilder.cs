using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class TimeEntryPoliciesRepositoryBuilder
{
    private readonly ITimeEntryPoliciesRepository _repository = Substitute.For<ITimeEntryPoliciesRepository>();

    public TimeEntryPoliciesRepositoryBuilder ListActiveByBranchIdAsNoTrackingReturns(
        Guid branchId,
        IReadOnlyList<TimeEntryPolicy> result)
    {
        _repository.ListActiveByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TimeEntryPoliciesRepositoryBuilder GetActiveByBranchIdAndEffectiveFromReturns(
        Guid branchId,
        DateTime effectiveFrom,
        TimeEntryPolicy? result)
    {
        _repository.GetActiveByBranchIdAndEffectiveFrom(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DateTime>(value => value == effectiveFrom),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public ITimeEntryPoliciesRepository Build()
    {
        return _repository;
    }
}
