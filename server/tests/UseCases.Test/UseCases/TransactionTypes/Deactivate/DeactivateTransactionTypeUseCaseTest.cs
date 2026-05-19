using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.TransactionTypes.Create;
using server.Application.UseCases.TransactionTypes.Deactivate;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.TransactionTypes.Deactivate;

public class DeactivateTransactionTypeUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldDeactivateTransactionType_WhenExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var tt = new TransactionTypeBuilder().WithActive(true).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId, tt)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        await useCase.Execute(tt.Id);

        tt.Active.ShouldBeFalse();
        await ttRepository.Received(1).GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDeactivateTransactionType_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var tt = new TransactionTypeBuilder().WithActive(true).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId, tt)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        await useCase.Execute(tt.Id);

        tt.Active.ShouldBeFalse();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(Guid.NewGuid()));

        await ttRepository.DidNotReceive().GetActiveByIdWithCategoryAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTransactionTypeNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var ttId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(ttId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ttId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenTransactionTypeIdIsEmpty()
    {
        var authenticationService = new AuthenticationServiceBuilder().Build();
        var ttRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_ID_EMPTY);
        await ttRepository.DidNotReceive().GetActiveByIdWithCategoryAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
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
            .GetActiveByIdWithCategoryAndBranchId(ttId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ttId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        await ttRepository.Received(1).GetActiveByIdWithCategoryAndBranchId(ttId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AllowsRecreateTransactionTypeWithSameName_AfterDeactivation()
    {
        // After deactivation the filtered unique index stops counting the row as a name reservation.
        // A new TransactionType with the same name in the same category must succeed with a different Id.
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var category = new CategoryBuilder().WithBranchId(branchUser.BranchId).Build();
        var name = "Tipo Reutilizado";
        var deactivatedTt = new TransactionTypeBuilder()
            .WithName(name)
            .WithCategory(category)
            .WithActive(true)
            .Build();

        var deactivateAuthService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var deactivateTtRepo = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(deactivatedTt.Id, branchUser.BranchId, deactivatedTt)
            .Build();
        var deactivateUow = new UnitOfWorkBuilder().Build();
        await new DeactivateTransactionTypeUseCase(deactivateAuthService, deactivateTtRepo, deactivateUow)
            .Execute(deactivatedTt.Id);
        deactivatedTt.Active.ShouldBeFalse();

        // ExistsActiveByCategoryIdAndName now returns false — deactivated row excluded.
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName(name)
            .Build();
        var newTt = new TransactionTypeBuilder().WithName(name).WithCategory(category).Build();
        var createAuthService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepo = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(category.Id, branchUser.BranchId, category)
            .Build();
        var createTtRepo = new TransactionTypesRepositoryBuilder()
            .ExistsActiveByCategoryIdAndName(category.Id, name, false)
            .Build();
        createTtRepo.GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Arg.Any<Guid>(), branchUser.BranchId)
            .Returns(newTt);
        var createUow = new UnitOfWorkBuilder().Build();

        var createResponse = await new CreateTransactionTypeUseCase(
            createAuthService, categoriesRepo, createTtRepo, createUow)
            .Execute(request);

        createResponse.Id.ShouldNotBe(deactivatedTt.Id);
        createResponse.Name.ShouldBe(name);
        await createTtRepo.Received(1).Add(Arg.Is<TransactionType>(tt =>
            tt.Id != deactivatedTt.Id && tt.Name == name && tt.Active));
        await createUow.Received(1).Commit();
    }

    private static DeactivateTransactionTypeUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ITransactionTypesRepository transactionTypesRepository,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateTransactionTypeUseCase(authenticationService, transactionTypesRepository, unitOfWork);
    }
}
