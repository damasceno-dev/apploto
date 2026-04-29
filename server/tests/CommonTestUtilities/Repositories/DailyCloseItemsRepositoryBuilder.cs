using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class DailyCloseItemsRepositoryBuilder
{
    private readonly IDailyCloseItemsRepository _repository = Substitute.For<IDailyCloseItemsRepository>();

    public DailyCloseItemsRepositoryBuilder ListActiveByDailyCloseIdAsNoTrackingReturns(
        Guid dailyCloseId,
        IReadOnlyList<DailyCloseItem> result)
    {
        _repository.ListActiveByDailyCloseIdAsNoTracking(Arg.Is<Guid>(value => value == dailyCloseId))
            .Returns(result);
        return this;
    }

    public IDailyCloseItemsRepository Build()
    {
        return _repository;
    }
}
