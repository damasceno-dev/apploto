namespace server.Infrastructure.Services;

public sealed record IdempotencyRequestCoordinationOptions(TimeSpan LockTimeout);
