using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Update;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Transactions.Update;

public class UpdateTransactionUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateEditableSubset_WhenMemberIsSameDayAndOwnOperator()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var beforeUpdate = DateTime.UtcNow;
        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request);
        var afterUpdate = DateTime.UtcNow;

        response.Id.ShouldBe(ctx.Transaction.Id);
        response.Description.ShouldBe(ctx.Request.Description);
        response.DueDate.ShouldBe(ctx.Request.DueDate);
        response.PaidAt.ShouldBe(ctx.Request.PaidAt);
        response.ClientId.ShouldBe(ctx.Request.ClientId);
        response.TransactionTime.ShouldBe(ctx.Request.TransactionTime);
        response.UpdatedAt.ShouldNotBeNull();
        response.UpdatedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
        response.UpdatedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
        response.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        ctx.Transaction.Description.ShouldBe(ctx.Request.Description);
        ctx.Transaction.DueDate.ShouldBe(ctx.Request.DueDate);
        ctx.Transaction.PaidAt.ShouldBe(ctx.Request.PaidAt);
        ctx.Transaction.ClientId.ShouldBe(ctx.Request.ClientId);
        ctx.Transaction.TransactionTime.ShouldBe(ctx.Request.TransactionTime);
        ctx.Transaction.UpdatedAt.ShouldBe(response.UpdatedAt);
        ctx.Transaction.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        await ctx.TransactionsRepository.Received(1)
            .GetByIdAndBranchId(ctx.Transaction.Id, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldClearNullableEditableFields_WhenRequestSuppliesNulls()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(null)
            .WithDueDate(ctx.Transaction.Date.AddDays(2))
            .WithPaidAt(null)
            .WithClientId(null)
            .WithTransactionTime(null)
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder().Build();

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request);

        response.Description.ShouldBeNull();
        response.PaidAt.ShouldBeNull();
        response.ClientId.ShouldBeNull();
        response.TransactionTime.ShouldBeNull();
        ctx.Transaction.Description.ShouldBeNull();
        ctx.Transaction.PaidAt.ShouldBeNull();
        ctx.Transaction.ClientId.ShouldBeNull();
        ctx.Transaction.TransactionTime.ShouldBeNull();
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenDueDateIsBeforeTransactionDate()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(-1))
            .WithPaidAt(ctx.Request.PaidAt)
            .WithClientId(ctx.Request.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenPaidAtIsBeforeTransactionDate()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Transaction.Date.AddDays(-1))
            .WithClientId(ctx.Request.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_PAID_AT_BEFORE_DATE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberTargetsUnlinkedAccount()
    {
        var ctx = BuildContext(Role.Member);
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(ctx.CallerOperator!.Id, [])
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberUpdatesOlderDayTransaction()
    {
        var ctx = BuildContext(Role.Member);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithDate(ctx.BranchClock.LocalBusinessDate(DateTime.UtcNow).AddDays(-1))
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Request.PaidAt)
            .WithClientId(ctx.Request.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldUseBranchClockForMemberSameDayDecision()
    {
        var ctx = BuildContext(Role.Member);
        var localBusinessDate = DateTime.UtcNow.Date.AddDays(-1);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithDate(localBusinessDate)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        ctx.BranchClock = Substitute.For<IBranchClock>();
        ctx.BranchClock
            .IsSameLocalDay(localBusinessDate, Arg.Any<DateTime>())
            .Returns(true);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Transaction.Date.AddDays(2))
            .WithClientId(ctx.Request.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.Transaction.Id, ctx.Request);

        ctx.BranchClock.Received(1).IsSameLocalDay(localBusinessDate, Arg.Any<DateTime>());
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberIsNotRecordedOperator()
    {
        var ctx = BuildContext(Role.Member);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithRecordedByOperatorId(Guid.NewGuid())
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_LINKED_OPERATOR);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberHasNoLinkedOperator()
    {
        var ctx = BuildContext(Role.Member);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_LINKED_OPERATOR);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public async Task Execute_ShouldAllowManagerInAnyPermissionMatrixCase(int dateOffsetDays, bool ownOperator)
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithDate(ctx.BranchClock.LocalBusinessDate(DateTime.UtcNow).AddDays(dateOffsetDays))
            .WithRecordedByOperatorId(ownOperator ? ctx.CallerOperator!.Id : Guid.NewGuid())
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Transaction.Date.AddDays(2))
            .WithClientId(ctx.Request.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request);

        response.Id.ShouldBe(ctx.Transaction.Id);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    public async Task Execute_ShouldAllowAdminInAnyPermissionMatrixCase(int dateOffsetDays, bool ownOperator)
    {
        var ctx = BuildContext(Role.Admin);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithDate(ctx.BranchClock.LocalBusinessDate(DateTime.UtcNow).AddDays(dateOffsetDays))
            .WithRecordedByOperatorId(ownOperator ? ctx.CallerOperator!.Id : Guid.NewGuid())
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Transaction.Date.AddDays(2))
            .WithClientId(ctx.Request.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request);

        response.Id.ShouldBe(ctx.Transaction.Id);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public async Task Execute_ShouldAllowElevatedRoleWithoutLinkedOperator(Role role)
    {
        var ctx = BuildContext(role);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithRecordedByOperatorId(Guid.NewGuid())
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request);

        response.Id.ShouldBe(ctx.Transaction.Id);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksTransactionDate()
    {
        var ctx = BuildContext(Role.Admin);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting
                {
                    BranchId = ctx.BranchUser.BranchId,
                    LockDate = ctx.Transaction.Date
                })
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenTransactionIsCancelled()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction.Status = TransactionStatus.Cancelled;
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_CANNOT_UPDATE_CANCELLED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTransactionIsMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager);
        var transactionId = Guid.NewGuid();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(transactionId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(transactionId, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
        await ctx.TransactionsRepository.Received(1).GetByIdAndBranchId(transactionId, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenFiadoTransactionClearsClient()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction.TransactionType.RequiresTabAccountAndClient = true;
        ctx.Transaction.ClientId = Guid.NewGuid();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Request.PaidAt)
            .WithClientId(null)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder().Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_FIADO_REQUIRES_CLIENT);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenRequestClientDoesNotExistInBranch()
    {
        var ctx = BuildContext(Role.Manager);
        var missingClientId = Guid.NewGuid();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Request.PaidAt)
            .WithClientId(missingClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(missingClientId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Transaction.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        await ctx.ClientsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(missingClientId, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private static UpdateTransactionUseCase CreateUseCase(TestContext ctx)
    {
        var memberTransactionScopeResolver = new MemberTransactionScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var lockDateGuard = new LockDateGuard(ctx.SettingsRepository);

        return new UpdateTransactionUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            ctx.ClientsRepository,
            ctx.UnitOfWork,
            memberTransactionScopeResolver,
            memberAccountScopeGuard,
            lockDateGuard,
            ctx.BranchClock);
    }

    private static TestContext BuildContext(Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var branch = new BranchBuilder().WithId(branchUser.BranchId).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranch(branch)
            .WithUserId(branchUser.UserId)
            .Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Entradas",
            DefaultDirection = Direction.In,
            BranchId = branch.Id,
            Branch = branch
        };
        var transactionType = new TransactionTypeBuilder()
            .WithCategory(category)
            .WithRequiresTabAccountAndClient(false)
            .Build();
        var branchClock = new BranchClock();
        var currentClientId = Guid.NewGuid();
        var requestClientId = Guid.NewGuid();
        var transactionDate = branchClock.LocalBusinessDate(DateTime.UtcNow);
        var transaction = new TransactionBuilder()
            .WithBranch(branch)
            .WithTransactionType(transactionType)
            .WithDate(transactionDate)
            .WithDueDate(transactionDate)
            .WithPaidAt(transactionDate.AddDays(1))
            .WithDescription("Original description")
            .WithClientId(currentClientId)
            .WithTransactionTime(new TimeOnly(9, 15))
            .WithRecordedByOperatorId(callerOperator.Id)
            .Build();
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("Updated description")
            .WithDueDate(transactionDate.AddDays(2))
            .WithPaidAt(transactionDate.AddDays(3))
            .WithClientId(requestClientId)
            .WithTransactionTime(new TimeOnly(15, 45))
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var transactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(transaction.Id, branchUser.BranchId, transaction)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(
                requestClientId,
                branchUser.BranchId,
                new ClientBuilder().WithId(requestClientId).WithBranchId(branchUser.BranchId).Build())
            .Build();
        var operatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(
                callerOperator.Id,
                [new OperatorAccountBuilder().WithOperator(callerOperator).WithAccountId(transaction.AccountId).Build()])
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        return new TestContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            Transaction = transaction,
            Request = request,
            AuthenticationService = authenticationService,
            TransactionsRepository = transactionsRepository,
            OperatorsRepository = operatorsRepository,
            ClientsRepository = clientsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            SettingsRepository = settingsRepository,
            BranchClock = branchClock,
            UnitOfWork = unitOfWork
        };
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator? CallerOperator { get; init; }
        public required Transaction Transaction { get; set; }
        public required RequestUpdateTransactionJson Request { get; set; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IClientsRepository ClientsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IBranchClock BranchClock { get; set; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }
}
