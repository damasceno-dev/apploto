using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Operators.Create;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Operators.Create;

public class CreateOperatorUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateOperator_WhenValidRequestWithoutUserId()
    {
        // Operators are branch-owned operational records and may exist before being linked to an app user.
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(null)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder().Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(request.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        response.UserId.ShouldBeNull();
        await operatorsRepository.Received(1).Add(Arg.Is<Operator>(op =>
            op.Name == request.Name.Trim() &&
            op.BranchId == branchUser.BranchId &&
            op.UserId == null &&
            op.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateOperator_WhenUserIdIsActiveBranchMember()
    {
        var targetUser = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .Build();
        var activeMembership = new BranchUserBuilder()
            .WithUserId(targetUser.Id)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(activeMembership)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.UserId.ShouldBe(targetUser.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkedUserDoesNotExist()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(Guid.NewGuid())
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(null)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.USER_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkedUserIsInactive()
    {
        var inactiveUser = new UserBuilder().WithActive(false).Build();
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(inactiveUser.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(inactiveUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.USER_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkedUserHasNoActiveBranchMembership()
    {
        var targetUser = new UserBuilder().Build();
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(targetUser.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(null)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_USER_NOT_BRANCH_MEMBER);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, new UsersRepositoryBuilder().Build(), new BranchUsersRepositoryBuilder().Build(), operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateOperatorUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IUsersRepository usersRepository,
        IBranchUsersRepository branchUsersRepository,
        IOperatorsRepository operatorsRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateOperatorUseCase(authenticationService, usersRepository, branchUsersRepository, operatorsRepository, unitOfWork);
    }
}
