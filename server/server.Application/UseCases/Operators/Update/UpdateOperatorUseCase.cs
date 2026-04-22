using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Operators.Update;

public class UpdateOperatorUseCase(
    IAuthenticationService authenticationService,
    IUsersRepository usersRepository,
    IBranchUsersRepository branchUsersRepository,
    IOperatorsRepository operatorsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseOperatorJson> Execute(Guid operatorId, RequestUpdateOperatorJson request)
    {
        Validate(operatorId, request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var op = await operatorsRepository.GetActiveByIdAndBranchId(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        if (request.UserId.HasValue)
        {
            await ValidateUserBranchMembership(request.UserId.Value, authenticatedBranchUser.BranchId);
            await EnsureUserLinkIsAvailable(request.UserId.Value, authenticatedBranchUser.BranchId, op.Id);
        }

        op.Name = request.Name.Trim();
        op.UserId = request.UserId;

        await unitOfWork.Commit();

        return op.ToOperatorResponse();
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

    private async Task EnsureUserLinkIsAvailable(Guid userId, Guid branchId, Guid operatorId)
    {
        var userAlreadyLinked = await operatorsRepository.ExistsActiveLinkedByUserIdAndBranchId(
            userId,
            branchId,
            operatorId);

        if (userAlreadyLinked)
        {
            throw new ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        }
    }

    private static void Validate(Guid operatorId, RequestUpdateOperatorJson request)
    {
        if (operatorId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.OPERATOR_ID_EMPTY]);
        }

        var result = new UpdateOperatorFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
