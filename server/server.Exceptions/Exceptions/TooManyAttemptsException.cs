namespace server.Exceptions.Exceptions;

/// <summary>429: an abuse budget is exhausted. <see cref="RetryAfter"/> is published as the Retry-After header.</summary>
public class TooManyAttemptsException(string message, TimeSpan retryAfter)
    : ServerException(message)
{
    public TimeSpan RetryAfter { get; } = retryAfter;

    public override int GetStatusCode => 429;

    public override List<string> GetErrorMessages => [Message];
}
