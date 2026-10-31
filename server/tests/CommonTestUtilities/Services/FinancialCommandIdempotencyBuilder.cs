using NSubstitute;
using server.Application.Services.Idempotency;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

/// <summary>A real <see cref="FinancialCommandIdempotency"/> over substitutes: every key is fresh and its lock is free.</summary>
public class FinancialCommandIdempotencyBuilder
{
    public IIdempotencyRequestsRepository Requests { get; } = Substitute.For<IIdempotencyRequestsRepository>();

    public FinancialCommandIdempotency Build()
    {
        var coordination = Substitute.For<IIdempotencyRequestCoordination>();
        coordination
            .TryAcquire(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IIdempotencyRequestCoordinationScope>());
        Requests
            .TryAcquireScopeLock(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return new FinancialCommandIdempotency(Requests, coordination, new CanonicalJsonRequestHasher());
    }
}
