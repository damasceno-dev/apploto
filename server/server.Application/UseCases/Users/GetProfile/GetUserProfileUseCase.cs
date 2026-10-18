using server.Application.UseCases.Users;
using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Users.GetProfile;

public sealed class GetUserProfileUseCase(IAuthenticationService authentication)
{
    public async Task<ResponseUserProfileJson> Execute() => (await authentication.GetAuthenticatedUser()).ToProfileResponse();
}
