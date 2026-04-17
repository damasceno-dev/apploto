using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Operators.List;
using server.Domain.Entities;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Operators.List;

public class ListOperatorsUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnOperators_WhenBranchHasActiveOperators()
    {
        var branchUser = new BranchUserBuilder().Build();
        var operators = new List<Operator>
        {
            new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Alpha").Build(),
            new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Beta").Build()
        }.AsReadOnly();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .ListActiveByBranchId(branchUser.BranchId, operators)
            .Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository);

        var response = await useCase.Execute();

        response.Operators.ShouldNotBeEmpty();
        response.Operators.Count.ShouldBe(2);
        response.Operators.ShouldAllBe(op => op.BranchId == branchUser.BranchId);
        await operatorsRepository.Received(1).ListActiveByBranchId(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenBranchHasNoOperators()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .ListActiveByBranchId(branchUser.BranchId, new List<global::server.Domain.Entities.Operator>().AsReadOnly())
            .Build();

        var useCase = CreateUseCase(authenticationService, operatorsRepository);

        var response = await useCase.Execute();

        response.Operators.ShouldBeEmpty();
        await operatorsRepository.Received(1).ListActiveByBranchId(branchUser.BranchId);
    }

    private static ListOperatorsUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IOperatorsRepository operatorsRepository)
    {
        return new ListOperatorsUseCase(authenticationService, operatorsRepository);
    }
}
