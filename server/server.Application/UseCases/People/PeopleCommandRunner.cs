using server.Application.Services.Idempotency;
using server.Application.Services.Security;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.People;

public class PeopleCommandRunner(
    IAuthenticationService authentication,
    IBranchUsersRepository memberships,
    ISecurityCoordination security,
    IMonthLockCoordination monthBoundary,
    IEmployeeRecordsRepository records,
    IOperatorsRepository operators,
    FinancialCommandIdempotency idempotency,
    IUnitOfWork unitOfWork,
    IBranchClock clock,
    ILockDateReader lockDate)
{
    internal static string Endpoint(string resource, Guid? previousId) => previousId is { } id
        ? $"POST /people/{resource}/{id}/revision"
        : $"POST /people/{resource}";

    public async Task<TResponse> Run<TRequest, TResponse>(
        string endpoint, TRequest request, string? key, uint? version, Guid? operatorId,
        Func<BranchUser, CancellationToken, Task> authorizeSources,
        Func<BranchUser, DateTime, CancellationToken, Task<(Guid Id, TResponse Response)>> execute,
        CancellationToken ct = default) where TRequest : RequestPeopleEvidenceJson where TResponse : class
    {
        var actor = await authentication.GetAuthenticatedBranchUser();
        if (actor.Role is not Role.Manager and not Role.Admin) throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        FinancialCommandIdempotency.EnsureValidKey(key);
        await using var boundary = await monthBoundary.TryAcquireShared(actor.BranchId, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.PEOPLE_STALE);
        await records.LockBranch(actor.BranchId, ct);
        await authorizeSources(actor, ct);
        await security.HoldSharedInCurrentTransaction(SecurityRules.BranchKey(actor.BranchId), ct);
        await memberships.LockMembershipsByBranchId(actor.BranchId, ct);
        actor = await SecurityRules.FreshActor(memberships, actor);
        if (operatorId is { } person && await operators.GetByIdAndBranchIdAsNoTracking(person, actor.BranchId, ct) is null)
            throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        // Source authority is checked even on an exact replay. Head/version/period checks apply only to a new write.
        var envelope = new { Request = request, Version = version };
        var context = await idempotency.Prepare<object, TResponse>(key!, endpoint, actor.BranchId, actor.UserId, envelope, clock.UtcNow(), ct);
        if (context.IsReplay) { await boundary.Complete(ct); return context.ReplayResponse!; }
        var lockedThrough = await lockDate.Read(actor.BranchId, ct);
        var today = clock.LocalBusinessDate(clock.UtcNow());
        if (request.PostedOn <= lockedThrough) throw new ConflictException(ResourcesErrorMessages.PEOPLE_PERIOD_LOCKED);
        if (request.PostedOn > today) throw new OnValidationException([ResourcesErrorMessages.PEOPLE_INPUT_INVALID]);
        var (id, response) = await execute(actor, lockedThrough, ct);
        FinancialCommandIdempotency.Complete(context, id, response);
        await unitOfWork.Commit(ct);
        await boundary.Complete(ct);
        return response;
    }
}
