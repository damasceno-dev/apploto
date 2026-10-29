using server.Application.Services.Members;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions;

internal static class SessionCustodyRules
{
    public static void EnsureReady(DailyClose session)
    {
        if (session.MappingState != SessionMappingState.Ready)
            throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE);
    }

    public static void EnsureActiveCustody(DailyClose session)
    {
        EnsureReady(session);
        if (session.CustodyState != SessionCustodyState.Active)
            throw new ConflictException(ResourcesErrorMessages.SESSION_NOT_ACTIVE_CUSTODY);
    }

    /// <summary>
    /// The custodian acts on their own session; a Manager/Admin may act for an absent custodian only as an explicit,
    /// reasoned exception. Returns true when the action is that exception.
    /// </summary>
    public static bool EnsureCustodianOrManagerException(
        DailyClose session,
        BranchUser actor,
        MemberAccountScope scope,
        string? reason)
    {
        if (scope.LinkedOperator?.Id == session.CustodianOperatorId)
            return false;

        if (actor.Role is not (Role.Manager or Role.Admin))
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.SESSION_NOT_CUSTODIAN);
        if (string.IsNullOrWhiteSpace(reason))
            throw new OnValidationException([ResourcesErrorMessages.SESSION_REASON_REQUIRED]);

        return true;
    }

    public static void EnsureManager(BranchUser actor)
    {
        if (actor.Role is not (Role.Manager or Role.Admin))
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }
}
