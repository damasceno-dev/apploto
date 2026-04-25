using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Operators;
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
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(request.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        response.UserId.ShouldBeNull();
        await operatorUserLinkGuard.DidNotReceiveWithAnyArgs().EnsureLinkable(Guid.Empty, Guid.Empty);
        await operatorsRepository.Received(1).Add(Arg.Is<Operator>(op =>
            op.Name == request.Name.Trim() &&
            op.BranchId == branchUser.BranchId &&
            op.UserId == null &&
            op.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateOperator_WhenUserIdIsPresentAndGuardPasses()
    {
        var targetUserId = Guid.NewGuid();
        var branchUser = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .Build();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(targetUserId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var response = await useCase.Execute(request);

        response.UserId.ShouldBe(targetUserId);
        await operatorUserLinkGuard.Received(1).EnsureLinkable(targetUserId, branchUser.BranchId);
        await operatorsRepository.Received(1).Add(Arg.Is<Operator>(op =>
            op.BranchId == branchUser.BranchId &&
            op.UserId == targetUserId &&
            op.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldPropagateGuardException_WhenLinkedUserIsNotLinkable()
    {
        var branchUser = new BranchUserBuilder().Build();
        var targetUserId = Guid.NewGuid();
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(targetUserId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder().Build();
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        operatorUserLinkGuard
            .EnsureLinkable(targetUserId, branchUser.BranchId)
            .Returns(Task.FromException(new ConflictException(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED)));
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED);
        await operatorUserLinkGuard.Received(1).EnsureLinkable(targetUserId, branchUser.BranchId);
        await operatorsRepository.DidNotReceive().Add(Arg.Any<Operator>());
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
        var operatorUserLinkGuard = CreateOperatorUserLinkGuard();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
        await operatorUserLinkGuard.DidNotReceiveWithAnyArgs().EnsureLinkable(Guid.Empty, Guid.Empty);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateOperatorUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IOperatorsRepository operatorsRepository,
        IOperatorUserLinkGuard operatorUserLinkGuard,
        IUnitOfWork unitOfWork)
    {
        return new CreateOperatorUseCase(authenticationService, operatorsRepository, operatorUserLinkGuard, unitOfWork);
    }

    private static IOperatorUserLinkGuard CreateOperatorUserLinkGuard()
    {
        var guard = Substitute.For<IOperatorUserLinkGuard>();

        guard.EnsureLinkable(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(Task.CompletedTask);
        return guard;
    }
}
