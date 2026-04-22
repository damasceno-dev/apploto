using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Operators.Update;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Operators.Update;

public class UpdateOperatorUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateName_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Old Name").Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithName("New Name")
            .WithUserId(null)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, new UsersRepositoryBuilder().Build(), new BranchUsersRepositoryBuilder().Build(), operatorsRepository, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        op.Name.ShouldBe("New Name");
        response.Name.ShouldBe("New Name");
        response.UserId.ShouldBeNull();
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldLinkUserId_WhenUserIsActiveBranchMember()
    {
        var targetUser = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var activeMembership = new BranchUserBuilder()
            .WithUserId(targetUser.Id)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser.Id, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(targetUser.Id, branchUser.BranchId, activeMembership)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        op.UserId.ShouldBe(targetUser.Id);
        response.UserId.ShouldBe(targetUser.Id);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await usersRepository.Received(1).GetById(targetUser.Id);
        await branchUsersRepository.Received(1).GetActiveByUserIdAndBranchId(targetUser.Id, branchUser.BranchId);
        await operatorsRepository.Received(1)
            .ExistsActiveLinkedByUserIdAndBranchId(targetUser.Id, branchUser.BranchId, op.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUnlinkUserId_WhenUserIdIsNull()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(Guid.NewGuid())
            .Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(null)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, new UsersRepositoryBuilder().Build(), new BranchUsersRepositoryBuilder().Build(), operatorsRepository, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        op.UserId.ShouldBeNull();
        response.UserId.ShouldBeNull();
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenOperatorIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestUpdateOperatorJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, new UsersRepositoryBuilder().Build(), new BranchUsersRepositoryBuilder().Build(), new OperatorsRepositoryBuilder().Build(), unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var operatorId = Guid.NewGuid();
        var request = new RequestUpdateOperatorJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(operatorId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, new UsersRepositoryBuilder().Build(), new BranchUsersRepositoryBuilder().Build(), operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(operatorId, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(operatorId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkedUserNotFound()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var targetUserId = Guid.NewGuid();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUserId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUserId, null)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, new BranchUsersRepositoryBuilder().Build(), operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.USER_NOT_FOUND);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await usersRepository.Received(1).GetById(targetUserId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkedUserHasNoActiveBranchMembership()
    {
        var targetUser = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser.Id, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(targetUser.Id, branchUser.BranchId, null)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_USER_NOT_BRANCH_MEMBER);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await usersRepository.Received(1).GetById(targetUser.Id);
        await branchUsersRepository.Received(1).GetActiveByUserIdAndBranchId(targetUser.Id, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenLinkedUserAlreadyHasActiveOperatorInBranch()
    {
        var targetUser = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var activeMembership = new BranchUserBuilder()
            .WithUserId(targetUser.Id)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser.Id, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(targetUser.Id, branchUser.BranchId, activeMembership)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .ExistsActiveLinkedByUserIdAndBranchId(targetUser.Id, branchUser.BranchId, true, op.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(op.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        op.UserId.ShouldBeNull();
        await operatorsRepository.Received(1)
            .ExistsActiveLinkedByUserIdAndBranchId(targetUser.Id, branchUser.BranchId, op.Id);
        await operatorsRepository.DidNotReceive().Add(Arg.Any<Operator>());
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateOperatorUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IUsersRepository usersRepository,
        IBranchUsersRepository branchUsersRepository,
        IOperatorsRepository operatorsRepository,
        IUnitOfWork unitOfWork)
    {
        return new UpdateOperatorUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);
    }
}
