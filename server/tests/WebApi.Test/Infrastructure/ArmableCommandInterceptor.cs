using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Test-only EF command interceptor on the host's database context (M11 Phase 7.5). Disarmed it does nothing; a test
/// arms it with a predicate and a hook that runs once, right after the first matching query returns its reader — the
/// seam between two overview sections, used to commit a concurrent write, fail or stall the request mid-way. While a
/// recording is open it also notes every command's text and whether the token it was executed with can be cancelled.
/// </summary>
public sealed class ArmableCommandInterceptor : DbCommandInterceptor
{
    private readonly Lock _gate = new();
    private Func<DbCommand, bool>? _match;
    private Func<CancellationToken, Task>? _hook;
    private List<RecordedCommand>? _recording;

    public RecordingScope Record()
    {
        lock (_gate)
            _recording = [];
        return new RecordingScope(this);
    }

    public IReadOnlyList<RecordedCommand> StopRecording()
    {
        lock (_gate)
        {
            var recorded = (IReadOnlyList<RecordedCommand>?)_recording?.ToList() ?? [];
            _recording = null;
            return recorded;
        }
    }

    private void Note(DbCommand command, CancellationToken cancellationToken)
    {
        lock (_gate)
            _recording?.Add(new RecordedCommand(command.CommandText, cancellationToken.CanBeCanceled));
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Note(command, cancellationToken);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Note(command, cancellationToken);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Note(command, cancellationToken);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public ArmedInterceptorScope Arm(Func<DbCommand, bool> match, Func<CancellationToken, Task> hook)
    {
        lock (_gate)
        {
            _match = match;
            _hook = hook;
        }

        return new ArmedInterceptorScope(this);
    }

    public void Disarm()
    {
        lock (_gate)
        {
            _match = null;
            _hook = null;
        }
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task>? hook = null;
        lock (_gate)
        {
            if (_match is not null && _hook is not null && _match(command))
            {
                hook = _hook;
                _match = null;
                _hook = null;
            }
        }

        if (hook is not null)
            await hook(cancellationToken);

        return result;
    }
}

public sealed class ArmedInterceptorScope(ArmableCommandInterceptor interceptor) : IDisposable
{
    public void Dispose() => interceptor.Disarm();
}

public sealed record RecordedCommand(string Sql, bool CanBeCanceled);

public sealed class RecordingScope(ArmableCommandInterceptor interceptor) : IDisposable
{
    public IReadOnlyList<RecordedCommand> Stop() => interceptor.StopRecording();
    public void Dispose() => interceptor.StopRecording();
}
