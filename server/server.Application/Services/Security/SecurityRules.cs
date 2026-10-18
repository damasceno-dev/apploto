using System.Security.Cryptography;
using System.Text;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Security;

public static class SecurityRules
{
    // Input is the canonical value returned by IUsersRepository.NormalizeEmail or persisted User/Invitation.Email.
    public static string EmailKey(string email) => "email:" + email;
    public static string BranchKey(Guid branchId) => "branch:" + branchId.ToString("N");
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static async Task<ISecurityScope> Acquire(ISecurityCoordination coordination, params string[] keys) =>
        await coordination.TryAcquire(keys) ?? throw new ConflictException(ResourcesErrorMessages.SECURITY_COORDINATION_BUSY);

    public static async Task<User> RequireUser(IAccountSecurityRepository security, Guid id) =>
        await security.GetUserForUpdate(id) ?? throw new TokenWithoutUserException(ResourcesErrorMessages.TOKEN_WITHOUT_USER);

    public static async Task<BranchUser> FreshActor(IBranchUsersRepository repository, BranchUser previous, bool memberOnly = false)
    {
        var actor = await repository.GetActiveById(previous.Id);
        if (actor is null || !actor.User.Active || actor.UserId != previous.UserId || actor.BranchId != previous.BranchId)
            throw new TokenInvalidException(ResourcesErrorMessages.TOKEN_INVALID);
        if (memberOnly ? actor.Role != Role.Member : actor.Role is not (Role.Admin or Role.Manager))
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        return actor;
    }
}
