namespace CommonTestUtilities.Services;

public sealed class FixedUtcTimeProvider(DateTimeOffset instant) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instant;
}
