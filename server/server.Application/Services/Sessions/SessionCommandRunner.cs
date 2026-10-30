using server.Application.Services.Idempotency;
using server.Application.Services.Members;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Application.Services.Sessions;

/// <summary>
/// The custody-command envelope (§6.20): the caller's current cashbox authority first — an inaccessible stored result
/// never leaks — then exact replay by Idempotency-Key before any lock (custody conditions are not rerun), then the cashbox
/// coordination, the persisted key reservation and the command body in one transaction. The body returns a
/// responder that maps the response after the first flush, so the replayed envelope carries the committed versions.
/// </summary>
public sealed class SessionCommandRunner(
    FinancialCommandIdempotency idempotency,
    IDailyCloseAccountCoordination accountCoordination,
    IUnitOfWork unitOfWork,
    IBranchClock branchClock,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard)
{
    public async Task<TResponse> Run<TFingerprint, TResponse>(
        string endpoint,
        string? idempotencyKey,
        BranchUser branchUser,
        TFingerprint fingerprint,
        IReadOnlyCollection<Guid> accountIds,
        Func<Task<Func<Task<(Guid ResourceId, TResponse Response)>>>> body,
        CancellationToken ct)
        where TResponse : class
    {
        var currentScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        foreach (var accountId in accountIds)
            memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, currentScope, accountId);

        var utcNow = branchClock.UtcNow();
        var replay = await idempotency.TryReplay<TFingerprint, TResponse>(
            idempotencyKey,
            endpoint,
            branchUser.BranchId,
            branchUser.UserId,
            fingerprint,
            utcNow,
            ct);
        if (replay is not null)
            return replay;

        await using var coordination = await accountCoordination.AcquireMany(branchUser.BranchId, accountIds, ct);
        var context = await idempotency.Prepare<TFingerprint, TResponse>(
            idempotencyKey!,
            endpoint,
            branchUser.BranchId,
            branchUser.UserId,
            fingerprint,
            utcNow,
            ct);
        if (context.IsReplay)
        {
            await coordination.Complete(ct);
            return context.ReplayResponse!;
        }

        var responder = await body();
        await unitOfWork.Commit(ct);
        var (resourceId, response) = await responder();
        FinancialCommandIdempotency.Complete(context, resourceId, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return response;
    }

    /// <summary>A coordinated command without idempotency (If-Match guarded corrections and attributions).</summary>
    public async Task<TResponse> RunGuarded<TResponse>(
        BranchUser branchUser,
        IReadOnlyCollection<Guid> accountIds,
        Func<Task<Func<Task<TResponse>>>> body,
        CancellationToken ct)
    {
        await using var coordination = await accountCoordination.AcquireMany(branchUser.BranchId, accountIds, ct);
        var responder = await body();
        await unitOfWork.Commit(ct);
        var response = await responder();
        await coordination.Complete(ct);
        return response;
    }
}
