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
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseRemoveBranchUserJson> Execute(Guid branchUserId)
    {
        Validate(branchUserId);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var targetBranchUser = await branchUsersRepository.GetById(branchUserId);

        if (targetBranchUser is null || targetBranchUser.Active is false || targetBranchUser.BranchId != authenticatedBranchUser.BranchId)
        {
            throw new NotFoundException(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);
        }

        if (BranchUserPermissionRules.CanManageMembership(authenticatedBranchUser.Role, targetBranchUser.Role) is false)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        await EnsureLastAdminInvariant(targetBranchUser);

        targetBranchUser.Active = false;
        await unitOfWork.Commit();

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
