using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.UseCases.DailyCloses.Get;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.DailyCloses.Get;

public class GetDailyCloseUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnDailyClose_WhenManagerReadsOwnBranchClose()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var item = new DailyCloseItemBuilder().Build();
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithStatus(DailyCloseStatus.Draft)
            .WithSubmittedByOperator(callerOperator)
            .WithItems([item])
            .Build();
        var ctx = BuildContext(branchUser, dailyClose);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(dailyClose.Id);

        response.Id.ShouldBe(dailyClose.Id);
        response.Date.ShouldBe(dailyClose.Date);
        response.Status.ShouldBe(dailyClose.Status);
        response.AccountId.ShouldBe(dailyClose.AccountId);
        response.AccountName.ShouldBe(account.Name);
        response.BranchId.ShouldBe(dailyClose.BranchId);
        response.SubmittedByOperatorId.ShouldBe(callerOperator.Id);
        response.SubmittedByOperatorName.ShouldBe(callerOperator.Name);
        response.Items.ShouldHaveSingleItem();
        response.Items[0].Id.ShouldBe(item.Id);
    }

    [Fact]
    public async Task Execute_ShouldSuppressDraftCashVarianceByResolvedId_WhenProductNavigationIsNotLoaded()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var cashVarianceProductId = Guid.NewGuid();
        var cashVarianceItem = new DailyCloseItem
        {
            ProductId = cashVarianceProductId,
            Product = null!,
            DailyCloseId = Guid.NewGuid(),
            DailyClose = null!,
            Value = 91m
        };
        var visibleItem = new DailyCloseItemBuilder().Build();
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithStatus(DailyCloseStatus.Draft)
            .WithItems([cashVarianceItem, visibleItem])
            .Build();
        var ctx = BuildContext(
            branchUser,
            dailyClose,
            cashVarianceProductId: cashVarianceProductId);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(dailyClose.Id);

        response.Items.ShouldHaveSingleItem().Id.ShouldBe(visibleItem.Id);
    }

    [Fact]
    public async Task Execute_ShouldProveExactBranchScopedArgToRepository()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var ctx = BuildContext(branchUser, dailyClose);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(dailyClose.Id);

        await ctx.DailyClosesRepository.Received(1)
            .GetByIdAndBranchIdAsNoTracking(dailyClose.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenCloseMissingOrCrossBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var missingId = Guid.NewGuid();
        var ctx = BuildContext(branchUser, dailyClose: null, dailyCloseId: missingId);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(missingId));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldReturnDailyClose_WhenAdminReadsOwnBranchClose()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var ctx = BuildContext(branchUser, dailyClose);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(dailyClose.Id);

        response.Id.ShouldBe(dailyClose.Id);
        await ctx.DailyClosesRepository.Received(1)
            .GetByIdAndBranchIdAsNoTracking(dailyClose.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenRequiresOperatorLink_WhenMemberHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var ctx = BuildContext(branchUser, dailyClose);
        // Default builders leave OperatorsRepository returning null operator.
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(dailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        // Close IS loaded before the scope check (404 first, then 403).
        await ctx.DailyClosesRepository.Received(1)
            .GetByIdAndBranchIdAsNoTracking(dailyClose.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenAccountOutOfScope_WhenMemberLinkedButDifferentAccount()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var linkedAccount = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var operatorAccount = new OperatorAccountBuilder()
            .WithOperator(callerOperator)
            .WithAccount(linkedAccount)
            .Build();

        // Close is on a different account than the one the operator is linked to.
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccountId(Guid.NewGuid())
            .Build();

        var ctx = BuildContext(branchUser, dailyClose);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(dailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task Execute_ShouldReturnDailyClose_WhenMemberLinkedAndAccountInScope()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var operatorAccount = new OperatorAccountBuilder()
            .WithOperator(callerOperator)
            .WithAccount(account)
            .Build();
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .Build();

        var ctx = BuildContext(branchUser, dailyClose);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(dailyClose.Id);

        response.Id.ShouldBe(dailyClose.Id);
        response.AccountId.ShouldBe(account.Id);
    }

    private static GetDailyCloseUseCase CreateUseCase(TestContext ctx)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);

        return new GetDailyCloseUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            memberAccountScopeResolver,
            new CashVarianceProductResolverBuilder()
                .ReturnsId(ctx.BranchUser.BranchId, ctx.CashVarianceProductId)
                .Build());
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        DailyClose? dailyClose,
        Guid? dailyCloseId = null,
        Guid? cashVarianceProductId = null)
    {
        var id = dailyCloseId ?? dailyClose?.Id ?? Guid.NewGuid();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingReturns(id, branchUser.BranchId, dailyClose)
            .Build();

        return new TestContext
        {
            BranchUser = branchUser,
            CashVarianceProductId = cashVarianceProductId ?? Guid.NewGuid(),
            AuthenticationService = authenticationService,
            DailyClosesRepository = dailyClosesRepository,
            OperatorsRepository = new OperatorsRepositoryBuilder().Build(),
            OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build()
        };
    }

    private class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Guid CashVarianceProductId { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
    }
}
