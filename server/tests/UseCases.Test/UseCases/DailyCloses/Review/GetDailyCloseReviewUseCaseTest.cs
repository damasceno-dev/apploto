using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.UseCases.DailyCloses.Review;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.DailyCloses.Review;

public class GetDailyCloseReviewUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldEnumerateEveryActiveProductWithNullClosings_WhenCloseIsFresh()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var firstProduct = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDisplayOrder(10)
            .Build();
        var secondProduct = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDisplayOrder(20)
            .Build();
        var cashVarianceProduct = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .WithDisplayOrder(30)
            .Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var context = BuildContext(
            branchUser,
            currentClose,
            priorClose: null,
            activeProducts: [secondProduct, cashVarianceProduct, firstProduct]);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        response.Items.Select(item => item.ProductId).ShouldBe([
            firstProduct.Id,
            secondProduct.Id,
            cashVarianceProduct.Id
        ]);
        response.Items.ShouldAllBe(item => item.ClosingValue == null);
        response.Items.Single(item => item.ProductId == cashVarianceProduct.Id).OpeningValue.ShouldBeNull();
        response.Items.Where(item => item.ProductId != cashVarianceProduct.Id)
            .ShouldAllBe(item => item.OpeningValue == 0m);
    }

    [Fact]
    public async Task Execute_ShouldUseZeroOpeningValue_WhenNoPriorCloseExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var currentItem = new DailyCloseItemBuilder()
            .WithProduct(product)
            .WithValue(125m)
            .Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithItems([currentItem])
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose: null);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        var item = response.Items.ShouldHaveSingleItem();
        item.ProductId.ShouldBe(product.Id);
        item.ProductName.ShouldBe(product.Name);
        item.DisplayOrder.ShouldBe(product.DisplayOrder);
        item.OpeningValue.ShouldBe(0m);
        item.ClosingValue.ShouldBe(125m);
        item.IsCashVarianceProduct.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ShouldKeepInactiveProduct_WhenCurrentCloseHasPersistedValue()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var retiredProduct = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDisplayOrder(15)
            .WithActive(false)
            .Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithItems([
                new DailyCloseItemBuilder()
                    .WithProduct(retiredProduct)
                    .WithValue(50m)
                    .Build()
            ])
            .Build();
        var context = BuildContext(
            branchUser,
            currentClose,
            priorClose: null,
            activeProducts: []);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        var item = response.Items.ShouldHaveSingleItem();
        item.ProductId.ShouldBe(retiredProduct.Id);
        item.ProductName.ShouldBe(retiredProduct.Name);
        item.DisplayOrder.ShouldBe(retiredProduct.DisplayOrder);
        item.OpeningValue.ShouldBe(0m);
        item.ClosingValue.ShouldBe(50m);
    }

    [Fact]
    public async Task Execute_ShouldUseMostRecentPriorCloseAcrossMissingCalendarDays()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var monday = new DateTime(2026, 7, 6);
        var friday = new DateTime(2026, 7, 3);
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithDate(monday)
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(product).WithValue(180m).Build()
            ])
            .Build();
        var priorClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithDate(friday)
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(product).WithValue(140m).Build()
            ])
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        response.Items.ShouldHaveSingleItem().OpeningValue.ShouldBe(140m);
        await context.DailyClosesRepository.Received(1)
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                branchUser.BranchId,
                account.Id,
                monday);
    }

    [Fact]
    public async Task Execute_ShouldExcludePriorOnlyProductAndUseZeroForNewCurrentProduct()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var removedProduct = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var newProduct = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(newProduct).WithValue(75m).Build()
            ])
            .Build();
        var priorClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithDate(currentClose.Date.AddDays(-4))
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(removedProduct).WithValue(90m).Build()
            ])
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        var item = response.Items.ShouldHaveSingleItem();
        item.ProductId.ShouldBe(newProduct.Id);
        item.OpeningValue.ShouldBe(0m);
        response.Items.ShouldNotContain(candidate => candidate.ProductId == removedProduct.Id);
    }

    [Fact]
    public async Task Execute_ShouldFlagCashVarianceAndKeepItsOpeningValueNull()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var cashVarianceProduct = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithStatus(DailyCloseStatus.Submitted)
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(cashVarianceProduct).WithValue(-12m).Build()
            ])
            .Build();
        var priorClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithDate(currentClose.Date.AddDays(-1))
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(cashVarianceProduct).WithValue(99m).Build()
            ])
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        var item = response.Items.ShouldHaveSingleItem();
        item.IsCashVarianceProduct.ShouldBeTrue();
        item.OpeningValue.ShouldBeNull();
        item.ClosingValue.ShouldBe(-12m);
    }

    [Fact]
    public async Task Execute_ShouldIgnoreInactivePriorItem()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(product).WithValue(50m).Build()
            ])
            .Build();
        var priorClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithDate(currentClose.Date.AddDays(-1))
            .WithItems([
                new DailyCloseItemBuilder()
                    .WithProduct(product)
                    .WithValue(200m)
                    .WithActive(false)
                    .Build()
            ])
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        response.Items.ShouldHaveSingleItem().OpeningValue.ShouldBe(0m);
    }

    [Fact]
    public async Task Execute_ShouldOrderItemsByDisplayOrderThenProductId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var firstProductId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondProductId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var zuluProduct = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Zulu")
            .WithDisplayOrder(2)
            .Build();
        var alphaSecondProduct = new ProductBuilder()
            .WithId(secondProductId)
            .WithBranchId(branchUser.BranchId)
            .WithName("Alpha")
            .WithDisplayOrder(1)
            .Build();
        var alphaFirstProduct = new ProductBuilder()
            .WithId(firstProductId)
            .WithBranchId(branchUser.BranchId)
            .WithName("Alpha")
            .WithDisplayOrder(1)
            .Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithItems([
                new DailyCloseItemBuilder().WithProduct(zuluProduct).Build(),
                new DailyCloseItemBuilder().WithProduct(alphaSecondProduct).Build(),
                new DailyCloseItemBuilder().WithProduct(alphaFirstProduct).Build()
            ])
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose: null);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        response.Items.Select(item => item.ProductId).ShouldBe([
            firstProductId,
            secondProductId,
            zuluProduct.Id
        ]);
    }

    [Fact]
    public async Task Execute_ShouldReturnReview_WhenMemberAccountIsInScope()
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
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose: null);
        context.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(
                branchUser.UserId,
                branchUser.BranchId,
                callerOperator)
            .Build();
        context.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(currentClose.Id);

        response.Id.ShouldBe(currentClose.Id);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenRequiresOperatorLink_WhenMemberHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose: null);
        var useCase = CreateUseCase(context);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(currentClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        await context.DailyClosesRepository.DidNotReceive()
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Execute_ShouldNotQueryPriorClose_WhenMemberAccountIsOutOfScope()
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
        var currentClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccountId(Guid.NewGuid())
            .Build();
        var context = BuildContext(branchUser, currentClose, priorClose: null);
        context.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(
                branchUser.UserId,
                branchUser.BranchId,
                callerOperator)
            .Build();
        context.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();
        var useCase = CreateUseCase(context);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(currentClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await context.DailyClosesRepository.DidNotReceive()
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Execute_ShouldReturnNotFoundBeforePriorLookup_WhenCloseIsMissingOrCrossBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var missingId = Guid.NewGuid();
        var context = BuildContext(branchUser, currentClose: null, priorClose: null, dailyCloseId: missingId);
        var useCase = CreateUseCase(context);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(missingId));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        await context.DailyClosesRepository.DidNotReceive()
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>());
    }

    private static GetDailyCloseReviewUseCase CreateUseCase(TestContext context)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            context.OperatorsRepository,
            context.OperatorAccountsRepository);

        return new GetDailyCloseReviewUseCase(
            context.AuthenticationService,
            context.DailyClosesRepository,
            context.ProductsRepository,
            memberAccountScopeResolver,
            context.CashVarianceProductResolver);
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        DailyClose? currentClose,
        DailyClose? priorClose,
        Guid? dailyCloseId = null,
        IReadOnlyList<Product>? activeProducts = null)
    {
        var id = dailyCloseId ?? currentClose?.Id ?? Guid.NewGuid();
        var repositoryBuilder = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingReturns(id, branchUser.BranchId, currentClose);

        if (currentClose is not null)
        {
            repositoryBuilder.GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTrackingReturns(
                branchUser.BranchId,
                currentClose.AccountId,
                currentClose.Date,
                priorClose);
        }

        activeProducts ??= currentClose?.Items
            .Where(item => item.Product is not null)
            .Select(item => item.Product!)
            .DistinctBy(product => product.Id)
            .OrderBy(product => product.DisplayOrder)
            .ThenBy(product => product.Id)
            .ToList() ?? [];
        var cashVarianceProductId = activeProducts
            .FirstOrDefault(product => product.Name == CashVarianceProductResolver.CashVarianceProductName)
            ?.Id ?? Guid.NewGuid();
        var cashVarianceProductResolver = Substitute.For<ICashVarianceProductResolver>();
        cashVarianceProductResolver
            .GetIdAsync(branchUser.BranchId, Arg.Any<CancellationToken>())
            .Returns(cashVarianceProductId);

        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            DailyClosesRepository = repositoryBuilder.Build(),
            ProductsRepository = new ProductsRepositoryBuilder()
                .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, activeProducts)
                .Build(),
            CashVarianceProductResolver = cashVarianceProductResolver,
            OperatorsRepository = new OperatorsRepositoryBuilder().Build(),
            OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build()
        };
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; init; }
        public required IProductsRepository ProductsRepository { get; init; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
    }
}
