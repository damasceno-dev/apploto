using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.TransactionTypes.List;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.TransactionTypes.List;

public class ListTransactionTypesUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnTransactionTypes_WhenBranchHasActiveItems()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var tt1 = new TransactionTypeBuilder().Build();
        var tt2 = new TransactionTypeBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, [tt1, tt2])
            .Build();

        var useCase = CreateUseCase(authenticationService, ttRepository);

        var response = await useCase.Execute();

        response.Items.Count.ShouldBe(2);
        await ttRepository.Received(1).ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenBranchHasNoActiveTransactionTypes()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, [])
            .Build();

        var useCase = CreateUseCase(authenticationService, ttRepository);

        var response = await useCase.Execute();

        response.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldScopeByBranch_WhenMultipleBranchesExist()
    {
        var branchUserA = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var ttA = new TransactionTypeBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUserA)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUserA.BranchId, [ttA])
            .Build();

        var useCase = CreateUseCase(authenticationService, ttRepository);

        var response = await useCase.Execute();

        response.Items.Count.ShouldBe(1);
        await ttRepository.Received(1).ListActiveByBranchIdAsNoTracking(branchUserA.BranchId);
        await ttRepository.DidNotReceive().ListActiveByBranchIdAsNoTracking(Arg.Is<Guid>(id => id != branchUserA.BranchId));
    }

    private static ListTransactionTypesUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ITransactionTypesRepository transactionTypesRepository)
    {
        return new ListTransactionTypesUseCase(authenticationService, transactionTypesRepository);
    }
}
