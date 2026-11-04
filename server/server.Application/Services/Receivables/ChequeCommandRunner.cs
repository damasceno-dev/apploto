using server.Application.Services.Idempotency;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Receivables;

/// <summary>
/// The envelope every cheque command runs in (M9.7 Phase 2). Order: the caller's current authority (Manager/Admin of
/// the branch) before any stored outcome is replayed; then the shared month boundary and every affected account key in
/// ascending order (the §6.6 global order, so a concurrent cancel, finalize or other cheque command on the same accounts
/// is serialized); then the persisted <c>Idempotency-Key</c> reservation; then the command, whose events, receipt and
/// replay envelope commit in one database transaction. Any failure rolls the whole transaction back, so a retry never
/// finds a partial effect or a stored success it did not get.
/// </summary>
public class ChequeCommandRunner(
    IAuthenticationService authenticationService,
    FinancialCommandIdempotency financialCommandIdempotency,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseChequeOperationJson> Run<TRequest>(
        string endpoint,
        TRequest request,
        string? idempotencyKey,
        Func<BranchUser, CancellationToken, Task<IReadOnlyCollection<Guid>>> resolveAccountIds,
        Func<BranchUser, CancellationToken, Task<(Guid ResourceId, ResponseChequeOperationJson Response)>> execute,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var utcNow = branchClock.UtcNow();
        var replay = await financialCommandIdempotency.TryReplay<TRequest, ResponseChequeOperationJson>(
            idempotencyKey, endpoint, branchUser.BranchId, branchUser.UserId, request, utcNow, ct);
        if (replay is not null)
            return replay;

        var accountIds = await resolveAccountIds(branchUser, ct);
        await using var coordination = await dailyCloseAccountCoordination.AcquireMany(branchUser.BranchId, accountIds.ToList(), ct);

        var idempotency = await financialCommandIdempotency.Prepare<TRequest, ResponseChequeOperationJson>(
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
