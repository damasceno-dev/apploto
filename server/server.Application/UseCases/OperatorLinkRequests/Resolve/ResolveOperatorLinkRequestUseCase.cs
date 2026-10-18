using server.Application.Services.Security;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.OperatorLinkRequests.Resolve;

public sealed class ResolveOperatorLinkRequestUseCase(
    IAuthenticationService authentication,
    IBranchUsersRepository memberships,
    IOnboardingRepository onboarding,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    OperatorLinkRequestReader reader)
{
    public async Task<ResponseOperatorLinkRequestJson> Execute(Guid id)
    {
        if (id == Guid.Empty) throw new OnValidationException([ResourcesErrorMessages.OPERATOR_REQUEST_NOT_FOUND]);
        var actor = await authentication.GetAuthenticatedBranchUser();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(actor.BranchId));
        actor = await SecurityRules.FreshActor(memberships, actor);
        var request = await onboarding.GetRequest(id, actor.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_REQUEST_NOT_FOUND);
        var state = await reader.State(request.UserId, request.BranchId);
        if (state.OperatorId is null) throw new ConflictException(ResourcesErrorMessages.OPERATOR_REQUEST_SETUP_REQUIRED);
        if (request.Status == OperatorLinkRequestStatus.Pending)
        {
            request.Status = OperatorLinkRequestStatus.Resolved;
            request.ResolvedAt = time.GetUtcNow().UtcDateTime;
            request.ResolvedByUserId = actor.UserId;
            await unitOfWork.Commit();
        }
        await scope.Complete();
        return OperatorLinkRequestReader.Map(request, request.User.Name, state);
    }
}
