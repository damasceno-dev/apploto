using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public sealed class MonthLockCoordinationBuilder
{
    private readonly IMonthLockCoordination _coordination = Substitute.For<IMonthLockCoordination>();

    public MonthLockCoordinationBuilder()
    {
        SharedScope = Substitute.For<IMonthLockCoordinationScope>();
        ExclusiveScope = Substitute.For<IMonthLockCoordinationScope>();
        _coordination.TryAcquireShared(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(SharedScope);
        _coordination.TryAcquireExclusive(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ExclusiveScope);
    }

    public IMonthLockCoordinationScope SharedScope { get; }
    public IMonthLockCoordinationScope ExclusiveScope { get; }

    public MonthLockCoordinationBuilder ExclusiveUnavailable()
    {
        _coordination.TryAcquireExclusive(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IMonthLockCoordinationScope?)null);
        return this;
    }

    public IMonthLockCoordination Build() => _coordination;
}
