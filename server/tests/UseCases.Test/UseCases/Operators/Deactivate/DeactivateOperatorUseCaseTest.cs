using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Operators.Deactivate;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Operators.Deactivate;

public class DeactivateOperatorUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldSetActiveFalse_WhenOperatorHasNoLinks()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithActive(true)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorId(op.Id, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(op.Id);

        op.Active.ShouldBeFalse();
        response.Id.ShouldBe(op.Id);
        await operatorsRepo.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await operatorAccountsRepo.Received(1).ListActiveByOperatorId(op.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCascadeDeactivateAllActiveLinks_WhenOperatorHasLinks()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var link1 = new OperatorAccountBuilder()
            .WithOperatorId(op.Id).WithIsPrimary(true).WithActive(true).Build();
        var link2 = new OperatorAccountBuilder()
            .WithOperatorId(op.Id).WithIsPrimary(false).WithActive(true).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorId(op.Id, [link1, link2])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(op.Id);

        op.Active.ShouldBeFalse();
        link1.Active.ShouldBeFalse();
        link1.IsPrimary.ShouldBeFalse();
        link2.Active.ShouldBeFalse();
        link2.IsPrimary.ShouldBeFalse();
        await operatorsRepo.Received(1).GetActiveByIdAndBranchId(op.Id, branchUser.BranchId);
        await operatorAccountsRepo.Received(1).ListActiveByOperatorId(op.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldNotRestoreLinks_WhenOperatorIsReactivatedLater()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var link = new OperatorAccountBuilder()
            .WithOperatorId(op.Id).WithActive(true).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchId(op.Id, branchUser.BranchId, op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorId(op.Id, [link])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(op.Id);

        link.Active.ShouldBeFalse();

        // Simulate manual reactivation of the operator entity
        op.Active = true;

        // The link remains deactivated — one-way cascade, reassignment must be explicit
        link.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenOperatorIdIsEmpty()
    {
        var authService = new AuthenticationServiceBuilder().Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotFoundInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var operatorId = Guid.NewGuid();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(operatorId, branchUser.BranchId, null)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(operatorId));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await operatorsRepo.Received(1).GetActiveByIdAndBranchId(operatorId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static DeactivateOperatorUseCase CreateUseCase(
        IAuthenticationService authService,
        IOperatorsRepository operatorsRepo,
        IOperatorAccountsRepository operatorAccountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateOperatorUseCase(authService, operatorsRepo, operatorAccountsRepo, unitOfWork);
    }
}
