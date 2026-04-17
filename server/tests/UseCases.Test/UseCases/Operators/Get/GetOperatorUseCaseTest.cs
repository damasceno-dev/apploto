using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Operators.Get;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Operators.Get;

public class GetOperatorUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnOperator_WhenFoundInAuthenticatedBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Test Operator")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId, op)
            .Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository);

        var response = await useCase.Execute(op.Id);

        response.Id.ShouldBe(op.Id);
        response.Name.ShouldBe(op.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotFoundInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var operatorId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(operatorId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(operatorId));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(operatorId, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorBelongsToDifferentBranch()
    {
        var branchUser = new BranchUserBuilder().WithBranchId(Guid.NewGuid()).Build();
        var operatorId = Guid.NewGuid();
        // Repository scopes to authenticated branch — cross-branch query returns null
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(operatorId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(operatorId));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await operatorsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(operatorId, branchUser.BranchId);
    }

    private static GetOperatorUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IOperatorsRepository operatorsRepository)
    {
        return new GetOperatorUseCase(authenticationService, operatorsRepository);
    }
}
