using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.TransactionTypes.Get;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.TransactionTypes.Get;

public class GetTransactionTypeUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnTransactionType_WhenFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var tt = new TransactionTypeBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchIdAsNoTracking(tt.Id, branchUser.BranchId, tt)
            .Build();

        var useCase = CreateUseCase(authenticationService, ttRepository);

        var response = await useCase.Execute(tt.Id);

        response.Id.ShouldBe(tt.Id);
        response.Name.ShouldBe(tt.Name);
        await ttRepository.Received(1).GetActiveByIdWithCategoryAndBranchIdAsNoTracking(tt.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTransactionTypeNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var ttId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchIdAsNoTracking(ttId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, ttRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ttId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTransactionTypeBelongsToDifferentBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var ttId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchIdAsNoTracking(ttId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, ttRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ttId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        await ttRepository.Received(1).GetActiveByIdWithCategoryAndBranchIdAsNoTracking(ttId, branchUser.BranchId);
    }

    private static GetTransactionTypeUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ITransactionTypesRepository transactionTypesRepository)
    {
        return new GetTransactionTypeUseCase(authenticationService, transactionTypesRepository);
    }
}
