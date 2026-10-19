using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.ResetPassword;

public sealed class ResetUserPasswordUseCase(
    IUsersRepository users,
    IAccountSecurityRepository security,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    AccountPasswordUpdater passwordUpdater)
{
    public async Task Execute(RequestResetPasswordJson request)
    {
        SecurityValidation.Validate(new ResetPasswordFluentValidation(), request);
        var hash = SecurityRules.Hash(request.Token);
        var candidate = await security.GetResetByHash(hash) ?? throw InvalidToken();
        var identity = await users.GetById(candidate.UserId) ?? throw InvalidToken();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(identity.Email));
        var token = await security.GetResetByHash(hash);
        var now = time.GetUtcNow().UtcDateTime;
        if (token is null || !token.Active || token.ConsumedAt.HasValue || token.ExpiresAt <= now) throw InvalidToken();
        var user = await security.GetUserForUpdate(identity.Id) ?? throw InvalidToken();
        await passwordUpdater.Execute(user, request.NewPassword);
        user.EmailVerifiedAt ??= now;
        token.ConsumedAt = now;
        await unitOfWork.Commit();
        await scope.Complete();
    }
    private static ConflictException InvalidToken() => new(ResourcesErrorMessages.SECURITY_TOKEN_INVALID);
}
