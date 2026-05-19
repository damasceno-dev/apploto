using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.TransactionTypes.Update;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.TransactionTypes.Update;

public class UpdateTransactionTypeUseCaseTest
{
    [Fact]
    public void ForwardOnlyGuarantee_UpdateUseCase_HasNoDependencyOnTransactionsRepository()
    {
        // The Update use case must NOT touch historical Transaction rows.
        // Absence of ITransactionsRepository in the constructor is the structural proof.
        var ctorParams = typeof(UpdateTransactionTypeUseCase)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Single()
            .GetParameters();

        ctorParams.Select(p => p.ParameterType)
            .ShouldNotContain(typeof(ITransactionsRepository));
    }

    [Fact]
    public async Task Execute_ForwardOnly_ExistingTransactionSnapshotIsUnchanged()
    {
        // A Transaction captures Direction and CategoryId as immutable denormalized
        // snapshots at create time (§6.1). Updating the parent TransactionType must not
        // touch those rows. The structural absence of ITransactionsRepository in the
        // constructor guarantees this; this test asserts the invariant on a live domain object.
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var tt = new TransactionTypeBuilder().Build();
        var snapshotCategoryId = tt.CategoryId;
        var snapshotDirection = tt.Category.DefaultDirection;

        var existingTransaction = new TransactionBuilder()
            .WithTransactionType(tt)
            .Build();

        var request = new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Renamed After Snapshot")
            .WithSettlementRule(SettlementRule.NextBusinessDay)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId, tt)
            .ExistsActiveByCategoryIdAndName(tt.CategoryId, "Renamed After Snapshot", false, tt.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        await useCase.Execute(tt.Id, request);

        tt.Name.ShouldBe("Renamed After Snapshot");
        tt.SettlementRule.ShouldBe(SettlementRule.NextBusinessDay);
        existingTransaction.CategoryId.ShouldBe(snapshotCategoryId);
        existingTransaction.Direction.ShouldBe(snapshotDirection);
    }

    [Fact]
    public async Task Execute_ShouldUpdateTransactionType_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var tt = new TransactionTypeBuilder().WithName("Old Name").Build();
        var request = new RequestUpdateTransactionTypeJsonBuilder().WithName("New Name").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId, tt)
            .ExistsActiveByCategoryIdAndName(tt.CategoryId, "New Name", false, tt.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        var response = await useCase.Execute(tt.Id, request);

        tt.Name.ShouldBe("New Name");
        tt.SettlementRule.ShouldBe(request.SettlementRule);
        tt.RequiresTabAccountAndClient.ShouldBe(request.RequiresTabAccountAndClient);
        response.Name.ShouldBe("New Name");
        await ttRepository.Received(1).GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateTransactionType_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var tt = new TransactionTypeBuilder().Build();
        var request = new RequestUpdateTransactionTypeJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId, tt)
            .ExistsActiveByCategoryIdAndName(tt.CategoryId, request.Name.Trim(), false, tt.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        await Should.NotThrowAsync(() => useCase.Execute(tt.Id, request));

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestUpdateTransactionTypeJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(Guid.NewGuid(), request));

        await ttRepository.DidNotReceive().GetActiveByIdWithCategoryAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTransactionTypeNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var ttId = Guid.NewGuid();
        var request = new RequestUpdateTransactionTypeJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(ttId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ttId, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenNameAlreadyUsedInCategory()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var tt = new TransactionTypeBuilder().Build();
        var request = new RequestUpdateTransactionTypeJsonBuilder().WithName("Taken Name").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdWithCategoryAndBranchId(tt.Id, branchUser.BranchId, tt)
            .ExistsActiveByCategoryIdAndName(tt.CategoryId, "Taken Name", true, tt.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(tt.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestUpdateTransactionTypeJsonBuilder().WithName(string.Empty).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_REQUIRED);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateTransactionTypeUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ITransactionTypesRepository transactionTypesRepository,
        IUnitOfWork unitOfWork)
    {
        return new UpdateTransactionTypeUseCase(authenticationService, transactionTypesRepository, unitOfWork);
    }
}
