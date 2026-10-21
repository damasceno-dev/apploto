namespace CommonTestUtilities.Services;

/// <summary>A UTC clock a test moves explicitly; timers still come from the base provider.</summary>
public sealed class SettableUtcTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Set(DateTimeOffset instant) => Interlocked.Exchange(ref _ticks, instant.UtcTicks);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
