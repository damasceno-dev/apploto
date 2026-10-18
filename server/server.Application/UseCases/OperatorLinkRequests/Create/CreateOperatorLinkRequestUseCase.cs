using server.Application.Services.Security;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.OperatorLinkRequests.Create;

public sealed class CreateOperatorLinkRequestUseCase(
    IAuthenticationService authentication,
    IBranchUsersRepository memberships,
    IOnboardingRepository onboarding,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    OperatorLinkRequestReader reader)
{
    public async Task<ResponseOperatorLinkRequestJson> Execute()
    {
        var actor = await authentication.GetAuthenticatedBranchUser();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(actor.BranchId));
        actor = await SecurityRules.FreshActor(memberships, actor, memberOnly: true);
        var state = await reader.State(actor.UserId, actor.BranchId);
        if (state.HasUsableAccount) throw new ConflictException(ResourcesErrorMessages.OPERATOR_REQUEST_NOT_NEEDED);
        var request = await onboarding.GetOwnRequest(actor.UserId, actor.BranchId);
        if (request is null || request.Status != OperatorLinkRequestStatus.Pending)
        {
            request = new OperatorLinkRequest { UserId = actor.UserId, BranchId = actor.BranchId, CreatedAt = time.GetUtcNow().UtcDateTime };
            await onboarding.AddRequest(request);
            await unitOfWork.Commit();
        }
        await scope.Complete();
        return OperatorLinkRequestReader.Map(request, actor.User.Name, state);
    }
}
