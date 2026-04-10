using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BranchUsers.UpdateRole;

public class UpdateBranchUserRoleUseCase(
    IAuthenticationService authenticationService,
    IBranchUsersRepository branchUsersRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseUpdateBranchUserRoleJson> Execute(Guid branchUserId, RequestUpdateBranchUserRoleJson request)
    {
        Validate(branchUserId, request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var targetBranchUser = await branchUsersRepository.GetById(branchUserId);

        if (targetBranchUser is null || targetBranchUser.Active is false || targetBranchUser.BranchId != authenticatedBranchUser.BranchId)
        {
            throw new NotFoundException(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);
        }

        var targetRole = request.Role!.Value;
        if (BranchUserPermissionRules.CanManageRole(authenticatedBranchUser.Role, targetBranchUser.Role, targetRole) is false)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        await EnsureLastAdminInvariant(targetBranchUser, targetRole);

        targetBranchUser.Role = targetRole;
        await unitOfWork.Commit();

        return targetBranchUser.ToUpdateResponse();
    }

    private async Task EnsureLastAdminInvariant(server.Domain.Entities.BranchUser targetBranchUser, Role targetRole)
    {
        if (targetBranchUser.Role != Role.Admin || targetRole == Role.Admin)
        {
            return;
        }

        var activeAdminCount = await branchUsersRepository.CountActiveAdminsByBranchId(targetBranchUser.BranchId);
        if (activeAdminCount <= 1)
        {
            throw new ConflictException(ResourcesErrorMessages.BRANCH_USER_LAST_ADMIN_CONFLICT);
        }
    }

    private static void Validate(Guid branchUserId, RequestUpdateBranchUserRoleJson request)
    {
        if (branchUserId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.BRANCH_USER_ID_EMPTY]);
        }

        var result = new UpdateBranchUserRoleFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
