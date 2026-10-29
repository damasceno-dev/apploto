using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public sealed class DailyCloseAccountCoordinationBuilder
{
    private readonly IDailyCloseAccountCoordination _coordination =
        Substitute.For<IDailyCloseAccountCoordination>();

    public DailyCloseAccountCoordinationBuilder()
    {
        Scope = Substitute.For<IDailyCloseAccountCoordinationScope>();
        _coordination
            .Acquire(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Scope);
        _coordination
            .AcquireMany(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Scope);
        _coordination
            .AcquireWithPairing(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Scope);
    }

    public IDailyCloseAccountCoordinationScope Scope { get; }

    public IDailyCloseAccountCoordination Build() => _coordination;
}
