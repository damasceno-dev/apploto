namespace server.Domain.Models;

public sealed record IdempotencyRequestScope(
    string Endpoint,
    Guid BranchId,
    Guid UserId,
    string Key);
