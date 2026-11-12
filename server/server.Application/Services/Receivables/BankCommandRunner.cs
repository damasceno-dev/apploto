using server.Application.Services.Idempotency;
using server.Application.Services.Transactions;
using server.Application.Services.Security;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Receivables;

/// <summary>
/// The envelope for provider settlement and bank evidence commands (M9.7 Phases 3–5). Order: the caller's current authority (Manager/Admin of
/// the branch) before any stored outcome is replayed; then the shared month boundary and every affected account key in
/// ascending order (the §6.6 global order, so a concurrent cancel, finalize or other financial command on the same accounts
/// is serialized); then the persisted <c>Idempotency-Key</c> reservation; then the command, whose events, receipt and
/// replay envelope commit in one database transaction. Any failure rolls the whole transaction back, so a retry never
/// finds a partial effect or a stored success it did not get.
/// </summary>
public class BankCommandRunner(
    IAuthenticationService authenticationService,
    FinancialCommandIdempotency financialCommandIdempotency,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    IBranchClock branchClock,
    IBankingReadLocks readLocks,
    IBranchUsersRepository branchUsersRepository,
    ISecurityCoordination securityCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<TResponse> Run<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        string? idempotencyKey,
        Func<BranchUser, CancellationToken, Task<IReadOnlyCollection<Guid>>> resolveAccountIds,
        Func<BranchUser, CancellationToken, Task<(Guid ResourceId, TResponse Response)>> execute,
        CancellationToken ct = default,
        bool coordinatePeople = false) where TResponse : class
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        branchUser = await SecurityRules.FreshActor(branchUsersRepository, branchUser);
        var utcNow = branchClock.UtcNow();
        var replay = await financialCommandIdempotency.TryReplay<TRequest, TResponse>(
            idempotencyKey, endpoint, branchUser.BranchId, branchUser.UserId, request, utcNow, ct);
        if (replay is not null)
            return replay;

        var accountIds = await resolveAccountIds(branchUser, ct);
        await using var coordination = coordinatePeople
            ? await dailyCloseAccountCoordination.AcquireForImport(branchUser.BranchId, accountIds.ToList(), ct)
            : await dailyCloseAccountCoordination.AcquireMany(branchUser.BranchId, accountIds.ToList(), ct);

        await readLocks.Accounts(branchUser.BranchId, accountIds, ct);
        await securityCoordination.HoldSharedInCurrentTransaction(SecurityRules.BranchKey(branchUser.BranchId), ct);
        await branchUsersRepository.LockMembershipsByBranchId(branchUser.BranchId, ct);
        branchUser = await SecurityRules.FreshActor(branchUsersRepository, branchUser);

        var idempotency = await financialCommandIdempotency.Prepare<TRequest, TResponse>(
            idempotencyKey!, endpoint, branchUser.BranchId, branchUser.UserId, request, utcNow, ct);
        if (idempotency.IsReplay)
        {
            await coordination.Complete(ct);
            return idempotency.ReplayResponse!;
        }

        var (resourceId, response) = await execute(branchUser, ct);
        FinancialCommandIdempotency.Complete(idempotency, resourceId, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return response;
    }
}
