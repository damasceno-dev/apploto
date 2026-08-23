namespace server.Infrastructure.Services;

public sealed record IdempotencyRequestCleanupOptions(
    TimeSpan SweepInterval,
    int BatchSize);
