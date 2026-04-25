using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.Operators;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Operators;

public class OperatorUserLinkGuardTest
{
    [Fact]
    public async Task EnsureLinkable_ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(userId, null)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var guard = CreateGuard(usersRepository, branchUsersRepository, operatorsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => guard.EnsureLinkable(userId, branchId));

        exception.Message.ShouldBe(ResourcesErrorMessages.USER_NOT_FOUND);
        await usersRepository.Received(1).GetById(userId);
        await branchUsersRepository.DidNotReceiveWithAnyArgs().GetActiveByUserIdAndBranchId(Guid.Empty, Guid.Empty);
        await operatorsRepository.DidNotReceiveWithAnyArgs().ExistsActiveLinkedByUserIdAndBranchId(Guid.Empty, Guid.Empty);
    }

    [Fact]
    public async Task EnsureLinkable_ShouldThrowNotFoundException_WhenUserIsInactive()
    {
        var user = new UserBuilder().WithActive(false).Build();
        var branchId = Guid.NewGuid();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user.Id, user)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var guard = CreateGuard(usersRepository, branchUsersRepository, operatorsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => guard.EnsureLinkable(user.Id, branchId));

        exception.Message.ShouldBe(ResourcesErrorMessages.USER_NOT_FOUND);
        await usersRepository.Received(1).GetById(user.Id);
        await branchUsersRepository.DidNotReceiveWithAnyArgs().GetActiveByUserIdAndBranchId(Guid.Empty, Guid.Empty);
        await operatorsRepository.DidNotReceiveWithAnyArgs().ExistsActiveLinkedByUserIdAndBranchId(Guid.Empty, Guid.Empty);
    }

    [Fact]
    public async Task EnsureLinkable_ShouldThrowNotFoundException_WhenUserIsNotActiveBranchMember()
    {
        var user = new UserBuilder().Build();
        var branchId = Guid.NewGuid();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user.Id, user)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(user.Id, branchId, null)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var guard = CreateGuard(usersRepository, branchUsersRepository, operatorsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => guard.EnsureLinkable(user.Id, branchId));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_USER_NOT_BRANCH_MEMBER);
        await usersRepository.Received(1).GetById(user.Id);
        await branchUsersRepository.Received(1).GetActiveByUserIdAndBranchId(user.Id, branchId);
        await operatorsRepository.DidNotReceiveWithAnyArgs().ExistsActiveLinkedByUserIdAndBranchId(Guid.Empty, Guid.Empty);
    }

    [Fact]
    public async Task EnsureLinkable_ShouldThrowConflictException_WhenUserAlreadyHasActiveOperatorInBranch()
    {
        var user = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder()
            .WithUserId(user.Id)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user.Id, user)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(user.Id, branchUser.BranchId, branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .ExistsActiveLinkedByUserIdAndBranchId(user.Id, branchUser.BranchId, true)
            .Build();
        var guard = CreateGuard(usersRepository, branchUsersRepository, operatorsRepository);

        var exception = await Should.ThrowAsync<ConflictException>(() => guard.EnsureLinkable(user.Id, branchUser.BranchId));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        await operatorsRepository.Received(1)
            .ExistsActiveLinkedByUserIdAndBranchId(user.Id, branchUser.BranchId, null);
    }

    [Fact]
    public async Task EnsureLinkable_ShouldPass_WhenExistingLinkIsExceptedOperator()
    {
        var user = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder()
            .WithUserId(user.Id)
            .Build();
        var exceptOperatorId = Guid.NewGuid();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user.Id, user)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(user.Id, branchUser.BranchId, branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .ExistsActiveLinkedByUserIdAndBranchId(user.Id, branchUser.BranchId, false, exceptOperatorId)
            .Build();
        var guard = CreateGuard(usersRepository, branchUsersRepository, operatorsRepository);

        await guard.EnsureLinkable(user.Id, branchUser.BranchId, exceptOperatorId);

        await usersRepository.Received(1).GetById(user.Id);
        await branchUsersRepository.Received(1).GetActiveByUserIdAndBranchId(user.Id, branchUser.BranchId);
        await operatorsRepository.Received(1)
            .ExistsActiveLinkedByUserIdAndBranchId(user.Id, branchUser.BranchId, exceptOperatorId);
    }

    [Fact]
    public async Task EnsureLinkable_ShouldPass_WhenUserIsActiveBranchMemberAndNotLinked()
    {
        var user = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder()
            .WithUserId(user.Id)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(user.Id, user)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(user.Id, branchUser.BranchId, branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .ExistsActiveLinkedByUserIdAndBranchId(user.Id, branchUser.BranchId, false)
            .Build();
        var guard = CreateGuard(usersRepository, branchUsersRepository, operatorsRepository);

        await guard.EnsureLinkable(user.Id, branchUser.BranchId);

        await usersRepository.Received(1).GetById(user.Id);
        await branchUsersRepository.Received(1).GetActiveByUserIdAndBranchId(user.Id, branchUser.BranchId);
        await operatorsRepository.Received(1)
            .ExistsActiveLinkedByUserIdAndBranchId(user.Id, branchUser.BranchId);
    }

    private static OperatorUserLinkGuard CreateGuard(
        IUsersRepository usersRepository,
        IBranchUsersRepository branchUsersRepository,
        IOperatorsRepository operatorsRepository)
    {
        return new OperatorUserLinkGuard(usersRepository, branchUsersRepository, operatorsRepository);
    }
}
