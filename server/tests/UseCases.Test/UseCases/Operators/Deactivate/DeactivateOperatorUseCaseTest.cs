using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Operators.Deactivate;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Operators.Deactivate;

public class DeactivateOperatorUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldSetActiveFalse_WhenOperatorExistsInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithActive(true)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(op)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, unitOfWork);

        var response = await useCase.Execute(op.Id);

        op.Active.ShouldBeFalse();
        response.Id.ShouldBe(op.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenOperatorIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, new OperatorsRepositoryBuilder().Build(), unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotFoundInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static DeactivateOperatorUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IOperatorsRepository operatorsRepository,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateOperatorUseCase(authenticationService, operatorsRepository, unitOfWork);
    }
}
