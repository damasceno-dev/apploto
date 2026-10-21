using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace server.Infrastructure.Services;

internal sealed class SecurityThrottleCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    SecurityThrottleCleanupOptions options,
    TimeProvider timeProvider,
    ILogger<SecurityThrottleCleanupHostedService> logger) : BackgroundService
{
    private static readonly EventId CompletedEvent = new(7721, "SecurityThrottleCleanupCompleted");
    private static readonly EventId FailedEvent = new(7722, "SecurityThrottleCleanupFailed");

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
                    var cleanup = scope.ServiceProvider.GetRequiredService<SecurityThrottleCleanup>();
                    var utcNow = timeProvider.GetUtcNow().UtcDateTime;
                    int login = 0, recovery = 0;
                    SecurityThrottleCleanupResult result;
                    do
                    {
                        result = await cleanup.DeleteExpiredBatch(utcNow, options.BatchSize, stoppingToken);
                        login += result.LoginDeleted;
                        recovery += result.RecoveryDeleted;
                    }
                    while (result.LoginDeleted == options.BatchSize || result.RecoveryDeleted == options.BatchSize);

                    if (login + recovery > 0)
                        logger.LogInformation(CompletedEvent,
                            "Deleted {LoginDeleted} expired login throttles and {RecoveryDeleted} elapsed recovery throttles.",
                            login, recovery);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(FailedEvent, exception,
                        "Failed to delete expired security throttles; the next scheduled sweep will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
    }
}
