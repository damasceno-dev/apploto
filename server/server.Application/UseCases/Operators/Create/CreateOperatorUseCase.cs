using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Operators.Create;

public class CreateOperatorUseCase(
    IAuthenticationService authenticationService,
    IUsersRepository usersRepository,
    IBranchUsersRepository branchUsersRepository,
    IOperatorsRepository operatorsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateOperatorJson> Execute(RequestCreateOperatorJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (request.UserId.HasValue)
        {
            await ValidateUserBranchMembership(request.UserId.Value, authenticatedBranchUser.BranchId);
            await EnsureUserLinkIsAvailable(request.UserId.Value, authenticatedBranchUser.BranchId);
        }

        var op = request.ToDomain(authenticatedBranchUser.BranchId);

        await operatorsRepository.Add(op);
        await unitOfWork.Commit();

        return op.ToResponse();
    }

    private async Task ValidateUserBranchMembership(Guid userId, Guid branchId)
    {
        var user = await usersRepository.GetById(userId);

        if (user is null || user.Active is false)
        {
            throw new NotFoundException(ResourcesErrorMessages.USER_NOT_FOUND);
        }

        var branchUser = await branchUsersRepository.GetActiveByUserIdAndBranchId(userId, branchId);

        if (branchUser is null)
        {
            throw new NotFoundException(ResourcesErrorMessages.OPERATOR_USER_NOT_BRANCH_MEMBER);
        }
    }

    private async Task EnsureUserLinkIsAvailable(Guid userId, Guid branchId)
    {
        var userAlreadyLinked = await operatorsRepository.ExistsActiveLinkedByUserIdAndBranchId(userId, branchId);

        if (userAlreadyLinked)
        {
            throw new ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        }
    }

    private static void Validate(RequestCreateOperatorJson request)
    {
        var result = new CreateOperatorFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
