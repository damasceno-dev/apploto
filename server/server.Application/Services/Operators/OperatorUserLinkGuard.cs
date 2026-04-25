using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Operators;

public sealed class OperatorUserLinkGuard(
    IUsersRepository usersRepository,
    IBranchUsersRepository branchUsersRepository,
    IOperatorsRepository operatorsRepository)
    : IOperatorUserLinkGuard
{
    public async Task EnsureLinkable(Guid userId, Guid branchId, Guid? exceptOperatorId = null)
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

        var userAlreadyLinked = await operatorsRepository.ExistsActiveLinkedByUserIdAndBranchId(
            userId,
            branchId,
            exceptOperatorId);

        if (userAlreadyLinked)
        {
            throw new ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        }
    }
}
