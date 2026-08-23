using System.Text.Json;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Idempotency;

public sealed class FinancialCommandIdempotency(
    IIdempotencyRequestsRepository requestsRepository,
    IIdempotencyRequestCoordination requestCoordination,
    CanonicalJsonRequestHasher requestHasher)
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaximumKeyLength = 128;
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TResponse?> TryReplay<TRequest, TResponse>(
        string? key,
        string endpoint,
        Guid branchId,
        Guid userId,
        TRequest payload,
        DateTime utcNow,
        CancellationToken ct = default)
        where TResponse : class
    {
        EnsureValidKey(key);
        var nonNullKey = key!;
        var payloadHash = requestHasher.Compute(payload);

        await using var coordination = await requestCoordination.TryAcquire(
                endpoint,
                branchId,
                userId,
                nonNullKey,
                ct)
            ?? throw new ConflictException(ResourcesErrorMessages.IDEMPOTENCY_COORDINATION_BUSY);
        var request = await requestsRepository.GetByScopeAsNoTracking(
            endpoint,
            branchId,
            userId,
            nonNullKey,
            ct);
        var replay = request is not null && request.ExpiresAt > utcNow
            ? DeserializeReplay<TResponse>(request, payloadHash)
            : null;
        await coordination.Complete(ct);
        return replay;
    }

    internal async Task<FinancialCommandIdempotencyContext<TResponse>> Prepare<TRequest, TResponse>(
        string key,
        string endpoint,
        Guid branchId,
        Guid userId,
        TRequest payload,
        DateTime utcNow,
        CancellationToken ct = default)
        where TResponse : class
    {
        var payloadHash = requestHasher.Compute(payload);

        if (await requestsRepository.TryAcquireScopeLock(endpoint, branchId, userId, key, ct) is false)
            throw new ConflictException(ResourcesErrorMessages.IDEMPOTENCY_COORDINATION_BUSY);
        var request = await requestsRepository.GetByScope(endpoint, branchId, userId, key, ct);

        if (request is not null && request.ExpiresAt > utcNow)
        {
            var replay = DeserializeReplay<TResponse>(request, payloadHash);
            return new FinancialCommandIdempotencyContext<TResponse>(request, replay);
        }

        if (request is null)
        {
            request = new IdempotencyRequest
            {
                Key = key,
                Endpoint = endpoint,
                PayloadHash = payloadHash,
                BranchId = branchId,
                UserId = userId,
                ExpiresAt = utcNow.Add(Retention)
            };
            await requestsRepository.Add(request, ct);
        }
        else
        {
            request.PayloadHash = payloadHash;
            request.ResourceId = Guid.Empty;
            request.ResponseEnvelope = string.Empty;
            request.CreatedAt = utcNow;
            request.ExpiresAt = utcNow.Add(Retention);
        }

        return new FinancialCommandIdempotencyContext<TResponse>(request, null);
    }

    private static TResponse DeserializeReplay<TResponse>(
        IdempotencyRequest request,
        string payloadHash)
        where TResponse : class
    {
        if (request.PayloadHash != payloadHash)
            throw new ConflictException(ResourcesErrorMessages.IDEMPOTENCY_KEY_PAYLOAD_CONFLICT);

        return JsonSerializer.Deserialize<TResponse>(request.ResponseEnvelope, JsonOptions)
            ?? throw new InvalidOperationException("Persisted idempotency response envelope is invalid.");
    }

    public static void Complete<TResponse>(
        FinancialCommandIdempotencyContext<TResponse> context,
        Guid resourceId,
        TResponse response)
        where TResponse : class
    {
        context.Request.ResourceId = resourceId;
        context.Request.ResponseEnvelope = JsonSerializer.Serialize(response, JsonOptions);
    }

    private static void EnsureValidKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new OnValidationException([ResourcesErrorMessages.IDEMPOTENCY_KEY_REQUIRED]);

        if (key.Length > MaximumKeyLength || key.Any(character => character is < '!' or > '~'))
            throw new OnValidationException([ResourcesErrorMessages.IDEMPOTENCY_KEY_INVALID]);
    }
}

public sealed record FinancialCommandIdempotencyContext<TResponse>(
    IdempotencyRequest Request,
    TResponse? ReplayResponse)
    where TResponse : class
{
    public bool IsReplay => ReplayResponse is not null;
}
