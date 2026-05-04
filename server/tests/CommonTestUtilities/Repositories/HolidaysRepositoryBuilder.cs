using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace CommonTestUtilities.Repositories;

public class HolidaysRepositoryBuilder
{
    private readonly IHolidaysRepository _repository = Substitute.For<IHolidaysRepository>();

    public HolidaysRepositoryBuilder GetByIdAndBranchIdReturns(
        Guid id,
        Guid branchId,
        Holiday? result)
    {
        _repository.GetByIdAndBranchId(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public HolidaysRepositoryBuilder GetByIdAndBranchIdAsNoTrackingReturns(
        Guid id,
        Guid branchId,
        Holiday? result)
    {
        _repository.GetByIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public HolidaysRepositoryBuilder GetActiveByBranchIdAndDateReturns(
        Guid branchId,
        DateTime date,
        Holiday? result)
    {
        _repository.GetActiveByBranchIdAndDate(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DateTime>(value => value == date))
            .Returns(result);
        return this;
    }

    public HolidaysRepositoryBuilder ListByBranchIdAsNoTrackingReturns(
        Guid branchId,
        HolidayListFilter filter,
        IReadOnlyList<Holiday> result)
    {
        _repository.ListByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<HolidayListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(result);
        return this;
    }

    public HolidaysRepositoryBuilder CountByBranchIdAsNoTrackingReturns(
        Guid branchId,
        HolidayListFilter filter,
        int count)
    {
        _repository.CountByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<HolidayListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(count);
        return this;
    }

    public HolidaysRepositoryBuilder ListActiveDatesByBranchIdAsNoTrackingReturns(
        Guid branchId,
        IReadOnlyList<DateOnly> result)
    {
        _repository.ListActiveDatesByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public IHolidaysRepository Build()
    {
        return _repository;
    }

    private static bool MatchesFilter(HolidayListFilter expected, HolidayListFilter actual)
    {
        return expected.Year == actual.Year &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.Page == actual.Page &&
               expected.PageSize == actual.PageSize;
    }
}
