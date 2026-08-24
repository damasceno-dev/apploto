using NSubstitute;
using server.Application.Services.Idempotency;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Idempotency;

public sealed class FinancialCommandIdempotencyTest
{
    [Fact]
    public async Task TryReplay_ShouldThrowIdempotencyConflict_WhenCoordinationTimesOut()
    {
        var requestsRepository = Substitute.For<IIdempotencyRequestsRepository>();
        var requestCoordination = Substitute.For<IIdempotencyRequestCoordination>();
        requestCoordination.TryAcquire(
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns((IIdempotencyRequestCoordinationScope?)null);
        var service = new FinancialCommandIdempotency(
            requestsRepository,
            requestCoordination,
            new CanonicalJsonRequestHasher());

        var exception = await Should.ThrowAsync<ConflictException>(() => service.TryReplay<object, object>(
            "held-key",
            "POST /transaction",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new { Value = 10m },
            DateTime.UtcNow));

        exception.Message.ShouldBe(ResourcesErrorMessages.IDEMPOTENCY_COORDINATION_BUSY);
    }

    [Fact]
    public async Task Prepare_ShouldThrowIdempotencyConflict_WhenScopeLockTimesOut()
    {
        var requestsRepository = Substitute.For<IIdempotencyRequestsRepository>();
        requestsRepository.TryAcquireScopeLock(
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
        var service = new FinancialCommandIdempotency(
            requestsRepository,
            Substitute.For<IIdempotencyRequestCoordination>(),
            new CanonicalJsonRequestHasher());

        var exception = await Should.ThrowAsync<ConflictException>(() => service.Prepare<object, object>(
            "held-key",
            "POST /transaction",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new { Value = 10m },
            DateTime.UtcNow));

        exception.Message.ShouldBe(ResourcesErrorMessages.IDEMPOTENCY_COORDINATION_BUSY);
    }
}
