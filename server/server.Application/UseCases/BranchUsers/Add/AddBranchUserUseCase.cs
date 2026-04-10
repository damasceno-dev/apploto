using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BranchUsers.Add;

public class AddBranchUserUseCase(
    IAuthenticationService authenticationService,
    IUsersRepository usersRepository,
    IBranchUsersRepository branchUsersRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseAddBranchUserJson> Execute(RequestAddBranchUserJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var targetRole = request.Role!.Value;

        if (BranchUserPermissionRules.CanAssignRole(authenticatedBranchUser.Role, targetRole) is false)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        var targetUser = await GetTargetUser(request);
        var existingBranchUser = await branchUsersRepository.GetByUserIdAndBranchId(targetUser.Id, authenticatedBranchUser.BranchId);

        if (existingBranchUser?.Active is true)
        {
            throw new ConflictException(ResourcesErrorMessages.BRANCH_USER_ALREADY_ACTIVE);
        }

        var branchUser = existingBranchUser ?? request.ToDomain(targetUser.Id, authenticatedBranchUser.BranchId);

        if (existingBranchUser is null)
        {
            await branchUsersRepository.Add(branchUser);
        }
        else
        {
            existingBranchUser.Active = true;
            existingBranchUser.Role = targetRole;
        }

        await unitOfWork.Commit();

        return branchUser.ToAddResponse(targetUser);
    }

    private async Task<User> GetTargetUser(RequestAddBranchUserJson request)
    {
        var user = request.UserId.HasValue
            ? await usersRepository.GetById(request.UserId.Value)
            : await usersRepository.GetByEmail(request.Email!.Trim());

        return user is not null && user.Active
            ? user
            : throw new NotFoundException(ResourcesErrorMessages.USER_NOT_FOUND);
    }

    private static void Validate(RequestAddBranchUserJson request)
    {
        var result = new AddBranchUserFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
