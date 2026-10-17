using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public sealed class AccountPairingCoordinationBuilder
{
    private readonly IAccountPairingCoordination _coordination = Substitute.For<IAccountPairingCoordination>();

    public AccountPairingCoordinationBuilder()
    {
        Scope = Substitute.For<IAccountPairingCoordinationScope>();
        _coordination.TryAcquire(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Scope);
    }

    public IAccountPairingCoordinationScope Scope { get; }

    public AccountPairingCoordinationBuilder Unavailable()
    {
        _coordination.TryAcquire(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IAccountPairingCoordinationScope?)null);
        return this;
    }

    public IAccountPairingCoordination Build() => _coordination;
}
