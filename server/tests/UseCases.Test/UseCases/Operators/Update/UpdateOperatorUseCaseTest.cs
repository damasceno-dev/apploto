using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Operators;
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
    public async Task Execute_ShouldUpdateName_WhenValidRequestWithoutUserId()
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
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        op.Name.ShouldBe("New Name");
        response.Name.ShouldBe("New Name");
        response.UserId.ShouldBeNull();
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await operatorUserLinkGuard.DidNotReceiveWithAnyArgs().EnsureLinkable(Guid.Empty, Guid.Empty);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldLinkUserId_WhenGuardPasses()
    {
        var targetUserId = Guid.NewGuid();
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUserId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        op.UserId.ShouldBe(targetUserId);
        response.UserId.ShouldBe(targetUserId);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await operatorUserLinkGuard.Received(1).EnsureLinkable(targetUserId, branchUser.BranchId, op.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldSucceed_WhenUserIdIsUnchanged()
    {
        var targetUserId = Guid.NewGuid();
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(targetUserId)
            .Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithName("Renamed Linked Operator")
            .WithUserId(targetUserId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        op.Name.ShouldBe("Renamed Linked Operator");
        op.UserId.ShouldBe(targetUserId);
        response.UserId.ShouldBe(targetUserId);
        await operatorUserLinkGuard.Received(1).EnsureLinkable(targetUserId, branchUser.BranchId, op.Id);
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
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        op.UserId.ShouldBeNull();
        response.UserId.ShouldBeNull();
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await operatorUserLinkGuard.DidNotReceiveWithAnyArgs().EnsureLinkable(Guid.Empty, Guid.Empty);
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
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, new OperatorsRepositoryBuilder().Build(), operatorUserLinkGuard, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
        await operatorUserLinkGuard.DidNotReceiveWithAnyArgs().EnsureLinkable(Guid.Empty, Guid.Empty);
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
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(operatorId, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchId(operatorId, branchUser.BranchId);
        await operatorUserLinkGuard.DidNotReceiveWithAnyArgs().EnsureLinkable(Guid.Empty, Guid.Empty);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldPropagateGuardException_WhenLinkedUserIsNotLinkable()
    {
        var targetUserId = Guid.NewGuid();
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(targetUserId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op)
            .Build();
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        operatorUserLinkGuard
            .EnsureLinkable(targetUserId, branchUser.BranchId, op.Id)
            .Returns(Task.FromException(new ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED)));
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(op.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        op.UserId.ShouldBeNull();
        await operatorUserLinkGuard.Received(1).EnsureLinkable(targetUserId, branchUser.BranchId, op.Id);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateOperatorUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IOperatorsRepository operatorsRepository,
        IOperatorUserLinkGuard operatorUserLinkGuard,
        IUnitOfWork unitOfWork)
    {
        return new UpdateOperatorUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);
    }

    private static IOperatorUserLinkGuard CreateOperatorUserLinkGuard()
    {
        var guard = Substitute.For<IOperatorUserLinkGuard>();

        guard.EnsureLinkable(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(Task.CompletedTask);
        return guard;
    }
}
