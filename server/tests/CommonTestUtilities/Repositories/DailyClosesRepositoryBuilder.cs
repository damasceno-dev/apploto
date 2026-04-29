using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace CommonTestUtilities.Repositories;

public class DailyClosesRepositoryBuilder
{
    private readonly IDailyClosesRepository _repository = Substitute.For<IDailyClosesRepository>();

    public DailyClosesRepositoryBuilder GetByIdAndBranchIdReturns(
        Guid id,
        Guid branchId,
        DailyClose? result)
    {
        _repository.GetByIdAndBranchId(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetByIdAndBranchIdAsNoTrackingReturns(
        Guid id,
        Guid branchId,
        DailyClose? result)
    {
        _repository.GetByIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTrackingReturns(
        Guid branchId,
        Guid accountId,
        DateTime beforeDate,
        DailyClose? result)
    {
        _repository.GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == accountId),
                Arg.Is<DateTime>(value => value == beforeDate))
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder ListByBranchIdAsNoTrackingReturns(
        Guid branchId,
        DailyCloseListFilter filter,
        IReadOnlyList<DailyClose> result)
    {
        _repository.ListByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder CountByBranchIdAsNoTrackingReturns(
        Guid branchId,
        DailyCloseListFilter filter,
        int count)
    {
        _repository.CountByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(count);
        return this;
    }

    public IDailyClosesRepository Build()
    {
        return _repository;
    }

    private static bool MatchesFilter(DailyCloseListFilter expected, DailyCloseListFilter actual)
    {
        return expected.AccountId == actual.AccountId &&
               expected.Status == actual.Status &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.OperatorId == actual.OperatorId &&
               expected.Mine == actual.Mine &&
               MatchesAllowedAccountIds(expected.AllowedAccountIds, actual.AllowedAccountIds) &&
               expected.Page == actual.Page &&
               expected.PageSize == actual.PageSize;
    }

    private static bool MatchesAllowedAccountIds(IReadOnlyList<Guid>? expected, IReadOnlyList<Guid>? actual)
    {
        return expected is null
            ? actual is null
            : actual is not null && expected.SequenceEqual(actual);
    }
}
