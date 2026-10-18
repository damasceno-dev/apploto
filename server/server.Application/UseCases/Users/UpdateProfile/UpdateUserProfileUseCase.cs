using server.Application.Services.Security;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Users.UpdateProfile;

public sealed class UpdateUserProfileUseCase(
    IAuthenticationService authentication,
    IAccountSecurityRepository security,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseUserProfileJson> Execute(RequestUpdateProfileJson request)
    {
        SecurityValidation.Validate(new UpdateProfileFluentValidation(), request);
        var identity = await authentication.GetAuthenticatedUser();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(identity.Email));
        var user = await SecurityRules.RequireUser(security, identity.Id);
        user.Name = request.Name.Trim();
        await unitOfWork.Commit();
        await scope.Complete();
        return user.ToProfileResponse();
    }
}
