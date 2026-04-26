using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Get;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Transactions.Get;

public class GetTransactionUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnTransaction_WhenManagerReadsOwnBranchTransaction()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var transaction = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithCreatedByUserId(branchUser.UserId)
            .WithCancelledAt(DateTime.UtcNow)
            .WithCancelledByUserId(Guid.NewGuid())
            .WithCancellationReason("Erro operacional")
            .WithStatus(TransactionStatus.Cancelled)
            .Build();
        var ctx = BuildContext(branchUser, transaction);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(transaction.Id);

        response.Id.ShouldBe(transaction.Id);
        response.Date.ShouldBe(transaction.Date);
        response.Value.ShouldBe(transaction.Value);
        response.Description.ShouldBe(transaction.Description);
        response.TransactionTime.ShouldBe(transaction.TransactionTime);
        response.TransactionTypeId.ShouldBe(transaction.TransactionTypeId);
        response.CategoryId.ShouldBe(transaction.CategoryId);
        response.Direction.ShouldBe(transaction.Direction);
        response.AccountId.ShouldBe(transaction.AccountId);
        response.ClientId.ShouldBe(transaction.ClientId);
        response.DueDate.ShouldBe(transaction.DueDate);
        response.PaidAt.ShouldBe(transaction.PaidAt);
        response.OriginTransactionId.ShouldBe(transaction.OriginTransactionId);
        response.RecordedByOperatorId.ShouldBe(transaction.RecordedByOperatorId);
        response.CreatedByUserId.ShouldBe(transaction.CreatedByUserId);
        response.Status.ShouldBe(transaction.Status);
        response.CancelledAt.ShouldBe(transaction.CancelledAt);
        response.CancelledByUserId.ShouldBe(transaction.CancelledByUserId);
        response.CancellationReason.ShouldBe(transaction.CancellationReason);
        response.BranchId.ShouldBe(transaction.BranchId);
        response.CreatedAt.ShouldBe(transaction.CreatedAt);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTransactionIsMissingOrCrossBranch()
    {
        var transactionId = Guid.NewGuid();
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var ctx = BuildContext(branchUser, transaction: null, transactionId);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(transactionId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenWithAccountOutOfScopeKey_WhenMemberTargetsUnlinkedAccount()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var linkedAccountId = Guid.NewGuid();
        var transaction = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccountId(Guid.NewGuid())
            .Build();
        var ctx = BuildContext(branchUser, transaction);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(
                callerOperator.Id,
                [new OperatorAccountBuilder().WithOperator(callerOperator).WithAccountId(linkedAccountId).Build()])
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(transaction.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ctx.OperatorAccountsRepository.Received(1).ListActiveByOperatorIdAsNoTracking(callerOperator.Id);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenWithRequiresOperatorLinkKey_WhenMemberHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var transaction = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var ctx = BuildContext(branchUser, transaction);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(transaction.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        await ctx.TransactionsRepository.DidNotReceive()
            .GetByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenMemberWithLinkedOperatorTargetsMissingTransaction()
    {
        // Missing/cross-branch ids stay 404 even for Members so attackers can't probe
        // for transaction existence outside their branch via account-scope vs not-found.
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var transactionId = Guid.NewGuid();
        var ctx = BuildContext(branchUser, transaction: null, transactionId);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(
                callerOperator.Id,
                [new OperatorAccountBuilder().WithOperator(callerOperator).WithAccountId(Guid.NewGuid()).Build()])
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(transactionId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }

    private static GetTransactionUseCase CreateUseCase(TestContext ctx)
    {
        var memberTransactionScopeResolver = new MemberTransactionScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);

        return new GetTransactionUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            memberTransactionScopeResolver);
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        Transaction? transaction,
        Guid? transactionId = null)
    {
        var id = transactionId ?? transaction?.Id ?? Guid.NewGuid();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var transactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingReturns(id, branchUser.BranchId, transaction)
            .Build();

        return new TestContext
        {
            AuthenticationService = authenticationService,
            TransactionsRepository = transactionsRepository,
            OperatorsRepository = new OperatorsRepositoryBuilder().Build(),
            OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build()
        };
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
    }
}
