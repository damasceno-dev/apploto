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
    }

    public IDailyCloseAccountCoordinationScope Scope { get; }

    public IDailyCloseAccountCoordination Build() => _coordination;
}
