using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace CommonTestUtilities.Repositories;

public class TimeEntriesRepositoryBuilder
{
    private readonly ITimeEntriesRepository _repository = Substitute.For<ITimeEntriesRepository>();

    public TimeEntriesRepositoryBuilder GetByIdAndBranchIdReturns(
        Guid id,
        Guid branchId,
        TimeEntry? result)
    {
        _repository.GetByIdAndBranchId(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public TimeEntriesRepositoryBuilder GetByIdAndBranchIdAsNoTrackingReturns(
        Guid id,
        Guid branchId,
        TimeEntry? result)
    {
        _repository.GetByIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public TimeEntriesRepositoryBuilder GetByBranchIdOperatorIdAndDateReturns(
        Guid branchId,
        Guid operatorId,
        DateTime date,
        TimeEntry? result)
    {
        _repository.GetByBranchIdOperatorIdAndDate(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == operatorId),
                Arg.Is<DateTime>(value => value == date))
            .Returns(result);
        return this;
    }

    public TimeEntriesRepositoryBuilder ListByBranchIdAsNoTrackingReturns(
        Guid branchId,
        TimeEntryListFilter filter,
        IReadOnlyList<TimeEntry> result)
    {
        _repository.ListByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<TimeEntryListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(result);
        return this;
    }

    public TimeEntriesRepositoryBuilder CountByBranchIdAsNoTrackingReturns(
        Guid branchId,
        TimeEntryListFilter filter,
        int count)
    {
        _repository.CountByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<TimeEntryListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(count);
        return this;
    }

    public TimeEntriesRepositoryBuilder ExistsActiveByBranchIdOperatorIdAndDateReturns(
        Guid branchId,
        Guid operatorId,
        DateTime date,
        bool result)
    {
        _repository.ExistsActiveByBranchIdOperatorIdAndDate(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == operatorId),
                Arg.Is<DateTime>(value => value == date))
            .Returns(result);
        return this;
    }

    public ITimeEntriesRepository Build()
    {
        return _repository;
    }

    private static bool MatchesFilter(TimeEntryListFilter expected, TimeEntryListFilter actual)
    {
        return expected.OperatorId == actual.OperatorId &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.Status == actual.Status &&
               expected.Mine == actual.Mine &&
               MatchesAllowedOperatorIds(expected.AllowedOperatorIds, actual.AllowedOperatorIds) &&
               expected.Page == actual.Page &&
               expected.PageSize == actual.PageSize;
    }

    private static bool MatchesAllowedOperatorIds(IReadOnlyList<Guid>? expected, IReadOnlyList<Guid>? actual)
    {
        return expected is null
            ? actual is null
            : actual is not null && expected.SequenceEqual(actual);
    }
}
