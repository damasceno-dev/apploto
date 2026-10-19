using server.Application.Services;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.ChangePassword;

public sealed class ChangeUserPasswordUseCase(
    IAuthenticationService authentication,
    IAccountSecurityRepository security,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    PasswordEncryption passwords,
    AccountPasswordUpdater passwordUpdater)
{
    public async Task Execute(RequestChangePasswordJson request)
    {
        SecurityValidation.Validate(new ChangePasswordFluentValidation(), request);
        var identity = await authentication.GetAuthenticatedUser();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(identity.Email));
        var user = await SecurityRules.RequireUser(security, identity.Id);
        if (string.IsNullOrEmpty(user.Password) || !passwords.VerifyPassword(request.CurrentPassword, user.Password))
            throw new InvalidLoginException(ResourcesErrorMessages.PASSWORD_WRONG);
        await passwordUpdater.Execute(user, request.NewPassword);
        await unitOfWork.Commit();
        await scope.Complete();
    }
}
