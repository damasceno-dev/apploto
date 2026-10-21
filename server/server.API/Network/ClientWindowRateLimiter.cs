using System.Threading.RateLimiting;

namespace server.Network;

/// <summary>
/// Fixed-window permit counter for one client network. The window opens with the first permit taken after the previous
/// window ended, nothing is queued, and a refusal carries the exact time left as <see cref="MetadataName.RetryAfter"/>.
/// It reads the injected <see cref="TimeProvider"/> (the framework limiters read their own timers), so window edges are
/// testable without sleeping. Process-local: each API instance counts on its own and a restart starts over.
/// </summary>
internal sealed class ClientWindowRateLimiter(TimeProvider time, int permitLimit, TimeSpan window) : RateLimiter
{
    private readonly Lock _gate = new();
    private DateTimeOffset _windowEnds = DateTimeOffset.MinValue;
    private int _used;
    private long _successful;
    private long _failed;

    // Lets the framework drop this partition once its window has ended, bounding memory to recently active networks.
    public override TimeSpan? IdleDuration
    {
        get
        {
            lock (_gate)
            {
                var now = time.GetUtcNow();
                return now >= _windowEnds ? now - _windowEnds : null;
            }
        }
    }

    public override RateLimiterStatistics GetStatistics()
    {
        lock (_gate)
        {
            return new RateLimiterStatistics
            {
                CurrentAvailablePermits = time.GetUtcNow() >= _windowEnds ? permitLimit : permitLimit - _used,
                CurrentQueuedCount = 0,
                TotalSuccessfulLeases = _successful,
                TotalFailedLeases = _failed
            };
        }
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(permitCount, permitLimit);
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (now >= _windowEnds) _used = 0;
            if (_used + permitCount <= permitLimit)
            {
                if (permitCount > 0 && _used == 0) _windowEnds = now + window;
                _used += permitCount;
                _successful++;
                return Lease.Acquired;
            }

            _failed++;
            return new Lease(false, _windowEnds - now);
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(permitCount));

    private sealed class Lease(bool acquired, TimeSpan? retryAfter) : RateLimitLease
    {
        public static readonly Lease Acquired = new(true, null);

        public override bool IsAcquired => acquired;

        public override IEnumerable<string> MetadataNames =>
            retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter is { } value && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = value;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
