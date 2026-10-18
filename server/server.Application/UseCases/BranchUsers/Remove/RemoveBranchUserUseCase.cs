using server.Application.Services.Security;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BranchUsers.Remove;

public class RemoveBranchUserUseCase(
    IAuthenticationService authenticationService,
    IBranchUsersRepository branchUsersRepository,
    IOperatorsRepository operatorsRepository,
    IUnitOfWork unitOfWork,
    ISecurityCoordination coordination)
{
    public async Task<ResponseRemoveBranchUserJson> Execute(Guid branchUserId)
    {
        Validate(branchUserId);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.BranchKey(authenticatedBranchUser.BranchId));
        authenticatedBranchUser = await SecurityRules.FreshActor(branchUsersRepository, authenticatedBranchUser);
        var targetBranchUser = await branchUsersRepository.GetById(branchUserId);

        if (targetBranchUser is null || targetBranchUser.Active is false || targetBranchUser.BranchId != authenticatedBranchUser.BranchId)
        {
            throw new NotFoundException(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);
        }

        if (targetBranchUser.UserId == authenticatedBranchUser.UserId)
            throw new ConflictException(ResourcesErrorMessages.BRANCH_USER_SELF_MEMBERSHIP_CONFLICT);

        if (BranchUserPermissionRules.CanManageMembership(authenticatedBranchUser.Role, targetBranchUser.Role) is false)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        await EnsureLastAdminInvariant(targetBranchUser);

        var linkedOperators = await operatorsRepository.ListByUserIdAndBranchId(
            targetBranchUser.UserId,
            targetBranchUser.BranchId);
        foreach (var linkedOperator in linkedOperators)
            linkedOperator.UserId = null;

        targetBranchUser.Active = false;
        await unitOfWork.Commit();
        await scope.Complete();

        return targetBranchUser.ToRemoveResponse();
    }

    private async Task EnsureLastAdminInvariant(BranchUser targetBranchUser)
    {
        if (targetBranchUser.Role != Role.Admin)
        {
            return;
        }

        var activeAdminCount = await branchUsersRepository.CountActiveAdminsByBranchId(targetBranchUser.BranchId);
        if (activeAdminCount <= 1)
        {
            throw new ConflictException(ResourcesErrorMessages.BRANCH_USER_LAST_ADMIN_CONFLICT);
        }
    }

    private static void Validate(Guid branchUserId)
    {
        if (branchUserId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.BRANCH_USER_ID_EMPTY]);
        }
    }
}
