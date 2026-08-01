using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.DailyCloses.VariancePreview;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.DailyCloses.VariancePreview;

public class PreviewDailyCloseVarianceUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldPassUnsavedCandidateValuesToSharedCalculator()
    {
        var context = BuildContext(Role.Manager);
        var useCase = CreateUseCase(context);

        var response = await useCase.Execute(context.Close.Id, context.Request);

        response.CashVariance.ShouldBe(context.CalculatedVariance);
        await context.CashVarianceCalculator.Received(1).CalculateCandidateAsync(
            context.BranchUser.BranchId,
            context.Close.AccountId,
            context.Close.Date,
            Arg.Is<IReadOnlyList<RequestUpsertDailyCloseItemJson>>(items =>
                items.Count == 1 &&
                items[0].ProductId == context.Product.Id &&
                items[0].Value == 250m),
            context.CashVarianceProductId,
            Arg.Any<CancellationToken>());
        await context.DailyClosesRepository.DidNotReceive()
            .GetByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_ShouldReturnNotFoundBeforeCalculation_WhenCloseIsMissingOrCrossBranch()
    {
        var context = BuildContext(Role.Manager);
        context.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingReturns(
                context.Close.Id,
                context.BranchUser.BranchId,
                null)
            .Build();
        var useCase = CreateUseCase(context);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(context.Close.Id, context.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        await context.CashVarianceCalculator.DidNotReceiveWithAnyArgs()
            .CalculateCandidateAsync(
                Guid.Empty,
                Guid.Empty,
                default,
                [],
                Guid.Empty,
                CancellationToken.None);
    }

    [Fact]
    public async Task Execute_ShouldApplySubmitStatusRules()
    {
        var context = BuildContext(Role.Manager, DailyCloseStatus.Submitted);
        context.WorkflowGuard = new DailyCloseWorkflowGuard(new FixedBranchClock(context.Close.Date));
        var useCase = CreateUseCase(context);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(context.Close.Id, context.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_SUBMITTABLE);
        await context.CashVarianceCalculator.DidNotReceiveWithAnyArgs()
            .CalculateCandidateAsync(
                Guid.Empty,
                Guid.Empty,
                default,
                [],
                Guid.Empty,
                CancellationToken.None);
    }

    private static PreviewDailyCloseVarianceUseCase CreateUseCase(TestContext context)
    {
        var scopeResolver = new MemberAccountScopeResolver(
            context.OperatorsRepository,
            context.OperatorAccountsRepository);

        return new PreviewDailyCloseVarianceUseCase(
            context.AuthenticationService,
            context.DailyClosesRepository,
            context.ProductsRepository,
            scopeResolver,
            new MemberAccountScopeGuard(),
            context.WorkflowGuard,
            new LockDateGuard(context.SettingsRepository),
            context.CashVarianceProductResolver,
            context.CashVarianceCalculator);
    }

    private static TestContext BuildContext(
        Role role,
        DailyCloseStatus status = DailyCloseStatus.Draft)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = new DailyCloseBuilder()
            .WithAccount(account)
            .WithDate(new DateTime(2026, 7, 29))
            .WithStatus(status)
            .Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var cashVarianceProductId = Guid.NewGuid();
        var request = new RequestDailyCloseVariancePreviewJson
        {
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 250m }]
        };
        var workflowGuard = Substitute.For<IDailyCloseWorkflowGuard>();
        const decimal calculatedVariance = 42.50m;
        var calculator = Substitute.For<ICashVarianceCalculator>();
        calculator.CalculateCandidateAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<IReadOnlyList<RequestUpsertDailyCloseItemJson>>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(calculatedVariance);
        var productResolver = Substitute.For<ICashVarianceProductResolver>();
        productResolver.GetIdAsync(branchUser.BranchId, Arg.Any<CancellationToken>())
            .Returns(cashVarianceProductId);

        return new TestContext
        {
            BranchUser = branchUser,
            Close = close,
            Product = product,
            CashVarianceProductId = cashVarianceProductId,
            CalculatedVariance = calculatedVariance,
            Request = request,
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            DailyClosesRepository = new DailyClosesRepositoryBuilder()
                .GetByIdAndBranchIdAsNoTrackingReturns(close.Id, branchUser.BranchId, close)
                .Build(),
            ProductsRepository = new ProductsRepositoryBuilder()
                .ListActiveByIdsAndBranchIdAsNoTrackingReturns(
                    [product.Id],
                    branchUser.BranchId,
                    [product])
                .Build(),
            OperatorsRepository = new OperatorsRepositoryBuilder().Build(),
            OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build(),
            SettingsRepository = new SettingsRepositoryBuilder()
                .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
                .Build(),
            WorkflowGuard = workflowGuard,
            CashVarianceProductResolver = productResolver,
            CashVarianceCalculator = calculator
        };
    }

    private sealed class FixedBranchClock(DateTime today) : IBranchClock
    {
        public DateTime UtcNow() => today;
        public DateTime LocalBusinessDateTime(DateTime utcInstant) => utcInstant;
        public DateTime LocalBusinessDate(DateTime utcInstant) => utcInstant.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
            => localBusinessDate.Date == today.Date;
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required server.Domain.Entities.DailyClose Close { get; init; }
        public required server.Domain.Entities.Product Product { get; init; }
        public required Guid CashVarianceProductId { get; init; }
        public required decimal CalculatedVariance { get; init; }
        public required RequestDailyCloseVariancePreviewJson Request { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required IProductsRepository ProductsRepository { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; init; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; init; }
        public required ISettingsRepository SettingsRepository { get; init; }
        public required IDailyCloseWorkflowGuard WorkflowGuard { get; set; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; init; }
        public required ICashVarianceCalculator CashVarianceCalculator { get; init; }
    }
}
