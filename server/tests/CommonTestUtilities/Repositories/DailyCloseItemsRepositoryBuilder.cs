using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace CommonTestUtilities.Repositories;

public class DailyCloseItemsRepositoryBuilder
{
    private readonly IDailyCloseItemsRepository _repository = Substitute.For<IDailyCloseItemsRepository>();

    public DailyCloseItemsRepositoryBuilder()
    {
        _repository
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<VarianceTimeSeriesRow>());
    }

    public DailyCloseItemsRepositoryBuilder ListActiveByDailyCloseIdAsNoTrackingReturns(
        Guid dailyCloseId,
        IReadOnlyList<DailyCloseItem> result)
    {
        _repository.ListActiveByDailyCloseIdAsNoTracking(Arg.Is<Guid>(value => value == dailyCloseId))
            .Returns(result);
        return this;
    }

    public DailyCloseItemsRepositoryBuilder ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        Guid productId,
        Guid? accountId,
        DateTime dateFrom,
        DateTime dateTo,
        IReadOnlyList<VarianceTimeSeriesRow> result)
    {
        _repository
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(v => v == branchId),
                Arg.Is<Guid>(v => v == productId),
                Arg.Is<Guid?>(v => v == accountId),
                Arg.Is<DateTime>(v => v == dateFrom),
                Arg.Is<DateTime>(v => v == dateTo),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public IDailyCloseItemsRepository Build()
    {
        return _repository;
    }
}
