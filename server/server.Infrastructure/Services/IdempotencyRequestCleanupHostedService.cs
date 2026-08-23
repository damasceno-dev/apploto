using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace server.Infrastructure.Services;

internal sealed class IdempotencyRequestCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    IdempotencyRequestCleanupOptions options,
    TimeProvider timeProvider,
    ILogger<IdempotencyRequestCleanupHostedService> logger) : BackgroundService
{
    private static readonly EventId FailedEvent = new(7712, "IdempotencyRequestCleanupFailed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SweepInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var cleanup = scope.ServiceProvider.GetRequiredService<IdempotencyRequestCleanup>();
                    var utcNow = timeProvider.GetUtcNow().UtcDateTime;
                    IdempotencyRequestCleanupResult result;
                    do
                    {
                        result = await cleanup.DeleteExpiredBatch(
                            utcNow,
                            options.BatchSize,
                            stoppingToken);
                    }
                    while (result.CandidateCount == options.BatchSize);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        FailedEvent,
                        exception,
                        "Failed to delete expired idempotency requests; the next scheduled sweep will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
    }
}
