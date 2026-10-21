using server.Application.Services.Transactions;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Test-only settable instant shared by the host. While unset the clock reads real time, so every test that never
/// freezes it behaves exactly as under the production <see cref="BranchClock"/>; the collection runs tests serially.
/// </summary>
public sealed class FrozenClockState
{
    private DateTime? _frozenUtc;

    public DateTime? FrozenUtc
    {
        get => _frozenUtc;
        set => _frozenUtc = value is { } instant ? DateTime.SpecifyKind(instant, DateTimeKind.Utc) : null;
    }
}

/// <summary>
/// The test host's scoped <see cref="IBranchClock"/> (M11 Phase 7.5 seam): production conversions, with
/// <see cref="UtcNow"/> taken from <see cref="FrozenClockState"/> when a test froze it.
/// </summary>
public sealed class FrozenBranchClock(FrozenClockState state) : IBranchClock
{
    private readonly BranchClock _production = new();

    public DateTime UtcNow() => state.FrozenUtc ?? _production.UtcNow();
    public DateTime LocalBusinessDateTime(DateTime utcInstant) => _production.LocalBusinessDateTime(utcInstant);
    public DateTime LocalBusinessDate(DateTime utcInstant) => _production.LocalBusinessDate(utcInstant);
    public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant) => _production.IsSameLocalDay(localBusinessDate, utcInstant);
}

internal static class FrozenClockExtensions
{
    extension(ServerWebApplicationFactory factory)
    {
        /// <summary>Freezes the host clock at a branch-local wall time (America/Sao_Paulo) until the scope is disposed.</summary>
        public FrozenClockScope FreezeBranchLocalTime(DateTime branchLocal)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(branchLocal, DateTimeKind.Unspecified), zone);
            return factory.FreezeUtc(utc);
        }

        public FrozenClockScope FreezeUtc(DateTime utc)
        {
            var state = factory.Services.GetService(typeof(FrozenClockState)) as FrozenClockState
                ?? throw new InvalidOperationException("The test host has no FrozenClockState registration.");
            state.FrozenUtc = utc;
            return new FrozenClockScope(state);
        }
    }
}

public sealed class FrozenClockScope(FrozenClockState state) : IDisposable
{
    /// <summary>Moves the frozen instant, keeping the scope (A21 time advance).</summary>
    public void AdvanceTo(DateTime branchLocal)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        state.FrozenUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(branchLocal, DateTimeKind.Unspecified), zone);
    }

    public void Dispose() => state.FrozenUtc = null;
}
