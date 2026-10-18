using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Users.Logout;

public sealed class LogoutUserUseCase(
    IAuthenticationService authentication,
    IAccountSecurityRepository security,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork)
{
    public async Task Execute(RequestLogoutJson request)
    {
        SecurityValidation.Validate(new LogoutFluentValidation(), request);
        var user = await authentication.GetAuthenticatedUser();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(user.Email));
        await SecurityRules.RequireUser(security, user.Id);
        await security.RevokeRefreshToken(user.Id, request.RefreshToken);
        await unitOfWork.Commit();
        await scope.Complete();
    }
}
