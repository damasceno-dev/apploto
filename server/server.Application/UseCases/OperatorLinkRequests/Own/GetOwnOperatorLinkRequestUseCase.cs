using server.Application.Services.Security;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.UseCases.OperatorLinkRequests.Own;

public sealed class GetOwnOperatorLinkRequestUseCase(
    IAuthenticationService authentication,
    IOnboardingRepository onboarding,
    OperatorLinkRequestReader reader)
{
    public async Task<ResponseOwnOperatorLinkRequestJson> Execute()
    {
        var actor = await authentication.GetAuthorizedBranchUser(Role.Member);
        var request = await onboarding.GetOwnRequest(actor.UserId, actor.BranchId);
        return new() { Request = request is null ? null : OperatorLinkRequestReader.Map(request, actor.User.Name, await reader.State(actor.UserId, actor.BranchId)) };
    }
}
