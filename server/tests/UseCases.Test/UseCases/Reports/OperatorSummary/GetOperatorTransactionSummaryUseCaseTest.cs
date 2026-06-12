using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.UseCases.Reports.OperatorSummary;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.OperatorSummary;

public class GetOperatorTransactionSummaryUseCaseTest
{
    private static readonly DateTime DateFrom = new(2025, 1, 1);
    private static readonly DateTime DateTo = new(2025, 1, 31);

    // -------------------------------------------------------------------------
    // Happy: Manager queries any branch operator by explicit OperatorId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnSummary_WhenManagerQueriesAnyBranchOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var projection = BuildProjection();

        var ctx = BuildContext(branchUser);
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubSummary(targetOperator.Id, allowedAccountIds: null, projection);

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(targetOperator.Id)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.OperatorId.ShouldBe(targetOperator.Id);
        response.OperatorName.ShouldBe(targetOperator.Name);
        response.DateFrom.ShouldBe(DateFrom);
        response.DateTo.ShouldBe(DateTo);
        response.TotalTransactionCount.ShouldBe(projection.TotalCount);
        response.TotalInValue.ShouldBe(projection.TotalIn);
        response.TotalOutValue.ShouldBe(projection.TotalOut);
        response.NetValue.ShouldBe(projection.TotalIn - projection.TotalOut);
        response.ByCategory.Count.ShouldBe(projection.ByCategory.Count);

        await ctx.TransactionsRepository.Received(1).SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == targetOperator.Id),
            Arg.Is<DateTime>(v => v == DateFrom),
            Arg.Is<DateTime>(v => v == DateTo),
            Arg.Is<IReadOnlyList<Guid>?>(v => v == null));
    }

    // -------------------------------------------------------------------------
    // Happy: Member self via Mine = true — account scope narrows the query
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnOwnSummary_WhenMemberUsesMine()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var allowedAccountIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var projection = BuildProjection();

        var ctx = BuildContext(branchUser, linkedOperator, allowedAccountIds);
        ctx.StubSummary(linkedOperator.Id, allowedAccountIds, projection);

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(true)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.OperatorId.ShouldBe(linkedOperator.Id);
        response.OperatorName.ShouldBe(linkedOperator.Name);
        response.TotalTransactionCount.ShouldBe(projection.TotalCount);

        await ctx.TransactionsRepository.Received(1).SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == linkedOperator.Id),
            Arg.Is<DateTime>(v => v == DateFrom),
            Arg.Is<DateTime>(v => v == DateTo),
            Arg.Is<IReadOnlyList<Guid>?>(v => v != null && v.SequenceEqual(allowedAccountIds)));
    }

    // -------------------------------------------------------------------------
    // Happy: Member with explicit OperatorId equal to own linked operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnOwnSummary_WhenMemberSuppliesOwnOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var allowedAccountIds = new List<Guid> { Guid.NewGuid() };
        var projection = BuildProjection();

        var ctx = BuildContext(branchUser, linkedOperator, allowedAccountIds);
        ctx.StubSummary(linkedOperator.Id, allowedAccountIds, projection);

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(linkedOperator.Id)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.OperatorId.ShouldBe(linkedOperator.Id);
        response.TotalTransactionCount.ShouldBe(projection.TotalCount);

        await ctx.TransactionsRepository.Received(1).SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == linkedOperator.Id),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Is<IReadOnlyList<Guid>?>(v => v != null && v.SequenceEqual(allowedAccountIds)));
    }

    // -------------------------------------------------------------------------
    // Happy: linked Member omitting both Mine and OperatorId falls back to
    // their own linked operator (Phase 9.8 fallback)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnOwnSummary_WhenMemberOmitsMineAndOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var allowedAccountIds = new List<Guid> { Guid.NewGuid() };
        var projection = BuildProjection();

        var ctx = BuildContext(branchUser, linkedOperator, allowedAccountIds);
        ctx.StubSummary(linkedOperator.Id, allowedAccountIds, projection);

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(false)
            .WithOperatorId(null)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.OperatorId.ShouldBe(linkedOperator.Id);
        response.OperatorName.ShouldBe(linkedOperator.Name);
        response.TotalTransactionCount.ShouldBe(projection.TotalCount);

        await ctx.TransactionsRepository.Received(1).SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == linkedOperator.Id),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Is<IReadOnlyList<Guid>?>(v => v != null && v.SequenceEqual(allowedAccountIds)));
    }

    // -------------------------------------------------------------------------
    // 403: Member targeting another operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow403_WhenMemberSuppliesAnotherOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var ctx = BuildContext(branchUser, linkedOperator, [Guid.NewGuid()]);
        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var action = async () => await useCase.Execute(request);

        var exception = await action.ShouldThrowAsync<TokenWithoutPermissionException>();
        exception.Message.ShouldBe(ResourcesErrorMessages.REPORT_MEMBER_NOT_OWN_OPERATOR);
        await ctx.TransactionsRepository.DidNotReceive().SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());
    }

    // -------------------------------------------------------------------------
    // Empty short-circuit: Member with no linked operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnEmptySummary_WhenMemberHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var ctx = BuildContext(branchUser, linkedOperator: null, allowedAccountIds: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(true)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.OperatorId.ShouldBe(Guid.Empty);
        response.OperatorName.ShouldBe(string.Empty);
        response.DateFrom.ShouldBe(DateFrom);
        response.DateTo.ShouldBe(DateTo);
        response.TotalTransactionCount.ShouldBe(0);
        response.TotalInValue.ShouldBe(0m);
        response.TotalOutValue.ShouldBe(0m);
        response.NetValue.ShouldBe(0m);
        response.ByCategory.ShouldBeEmpty();

        await ctx.TransactionsRepository.DidNotReceive().SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());
    }

    // -------------------------------------------------------------------------
    // Empty short-circuit: no linked operator wins even over an explicit
    // OperatorId — empty summary, not 403, and still no repository call
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnEmptySummary_WhenUnlinkedMemberSuppliesExplicitOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var ctx = BuildContext(branchUser, linkedOperator: null, allowedAccountIds: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.OperatorId.ShouldBe(Guid.Empty);
        response.OperatorName.ShouldBe(string.Empty);
        response.TotalTransactionCount.ShouldBe(0);
        response.ByCategory.ShouldBeEmpty();

        await ctx.TransactionsRepository.DidNotReceive().SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());
    }

    // -------------------------------------------------------------------------
    // Empty short-circuit: linked Member with zero active account links
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnEmptySummary_WhenMemberHasNoLinkedAccounts()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var linkedOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var ctx = BuildContext(branchUser, linkedOperator, allowedAccountIds: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(true)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.OperatorId.ShouldBe(Guid.Empty);
        response.OperatorName.ShouldBe(string.Empty);
        response.TotalTransactionCount.ShouldBe(0);
        response.ByCategory.ShouldBeEmpty();

        await ctx.TransactionsRepository.DidNotReceive().SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());
    }

    // -------------------------------------------------------------------------
    // 404: Manager targeting a missing or cross-branch operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow404_WhenOperatorBelongsToAnotherBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var crossBranchOperatorId = Guid.NewGuid();

        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepositoryBuilder.GetActiveByIdAndBranchIdAsNoTracking(crossBranchOperatorId, branchUser.BranchId, null);

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(crossBranchOperatorId)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var action = async () => await useCase.Execute(request);

        var exception = await action.ShouldThrowAsync<NotFoundException>();
        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await ctx.TransactionsRepository.DidNotReceive().SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());
    }

    // -------------------------------------------------------------------------
    // 404: Manager using Mine = true without an own linked operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow404_WhenManagerUsesMineWithoutLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();

        var ctx = BuildContext(branchUser, linkedOperator: null, allowedAccountIds: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(true)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var action = async () => await useCase.Execute(request);

        var exception = await action.ShouldThrowAsync<NotFoundException>();
        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    // -------------------------------------------------------------------------
    // 400: Manager omitting both Mine and OperatorId — no implicit fallback
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenManagerOmitsMineAndOperatorId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();

        var ctx = BuildContext(branchUser);
        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(false)
            .WithOperatorId(null)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_OPERATOR_ID_REQUIRED);
        await ctx.TransactionsRepository.DidNotReceive().SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());
    }

    // -------------------------------------------------------------------------
    // Branch isolation: caller's branch id is the only one passed downstream
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldPassCallerBranchIdToRepositories()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var otherBranchId = Guid.NewGuid();

        var ctx = BuildContext(branchUser);
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubSummary(targetOperator.Id, allowedAccountIds: null, BuildProjection());

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(targetOperator.Id)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        await useCase.Execute(request);

        await ctx.OperatorsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == targetOperator.Id),
            Arg.Is<Guid>(v => v == branchUser.BranchId));

        await ctx.TransactionsRepository.Received(1).SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());

        await ctx.TransactionsRepository.DidNotReceive().SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == otherBranchId),
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IReadOnlyList<Guid>?>());
    }

    // -------------------------------------------------------------------------
    // Cancelled + Draft excluded — the projection contract only carries rows
    // with Status = Active, so the response reflects exactly that subset
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReflectOnlyActiveRows_WhenProjectionExcludesCancelledAndDraft()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        // The seeded matrix conceptually holds 2 Active + 1 Draft + 1 Cancelled rows;
        // the repository contract (Active = true AND Status = Active) surfaces only
        // the 2 Active rows in the projection.
        var categoryId = Guid.NewGuid();
        var projection = new OperatorTransactionSummaryProjection(
            TotalCount: 2,
            TotalIn: 100m,
            TotalOut: 40m,
            ByCategory: [new CategoryTotal(categoryId, "Loteria", 2, 100m, 40m)]);

        var ctx = BuildContext(branchUser);
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubSummary(targetOperator.Id, allowedAccountIds: null, projection);

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(targetOperator.Id)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.TotalTransactionCount.ShouldBe(2);
        response.TotalInValue.ShouldBe(100m);
        response.TotalOutValue.ShouldBe(40m);
        response.NetValue.ShouldBe(60m);
        response.ByCategory.Count.ShouldBe(1);
        response.ByCategory[0].Count.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // ByCategory ordering: CategoryName ASC, CategoryId ASC preserved exactly
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldPreserveCategoryOrdering_WhenProjectionIsOrderedByNameThenId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var targetOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var duplicateNameIdLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var duplicateNameIdHigh = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var orderedCategories = new List<CategoryTotal>
        {
            new(duplicateNameIdLow, "Apostas", 1, 10m, 0m),
            new(duplicateNameIdHigh, "Apostas", 2, 25m, 5m),
            new(Guid.NewGuid(), "Bolão", 3, 60m, 15m)
        };
        var projection = new OperatorTransactionSummaryProjection(6, 95m, 20m, orderedCategories);

        var ctx = BuildContext(branchUser);
        ctx.StubOperatorInBranch(targetOperator);
        ctx.StubSummary(targetOperator.Id, allowedAccountIds: null, projection);

        var useCase = CreateUseCase(ctx);
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(targetOperator.Id)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.ByCategory.Select(c => c.CategoryId)
            .ShouldBe(orderedCategories.Select(c => c.CategoryId));
        response.ByCategory.Select(c => c.CategoryName)
            .ShouldBe(orderedCategories.Select(c => c.CategoryName));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static GetOperatorTransactionSummaryUseCase CreateUseCase(TestContext ctx)
    {
        return new GetOperatorTransactionSummaryUseCase(
            ctx.AuthenticationService,
            ctx.OperatorsRepository,
            ctx.TransactionsRepository,
            ctx.MemberScopeResolver);
    }

    private static OperatorTransactionSummaryProjection BuildProjection()
    {
        var categories = new List<CategoryTotal>
        {
            new(Guid.NewGuid(), "Apostas", 3, 150m, 0m),
            new(Guid.NewGuid(), "Despesas", 2, 0m, 80m)
        };

        return new OperatorTransactionSummaryProjection(5, 150m, 80m, categories);
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        Operator? linkedOperator = null,
        IReadOnlyList<Guid>? allowedAccountIds = null)
    {
        var memberScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
        memberScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId)
            .Returns(new MemberAccountScope(linkedOperator, allowedAccountIds ?? []));

        var operatorsRepositoryBuilder = new OperatorsRepositoryBuilder();
        var transactionsRepositoryBuilder = new TransactionsRepositoryBuilder();

        return new TestContext
        {
            BranchUser = branchUser,
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            OperatorsRepositoryBuilder = operatorsRepositoryBuilder,
            OperatorsRepository = operatorsRepositoryBuilder.Build(),
            TransactionsRepositoryBuilder = transactionsRepositoryBuilder,
            TransactionsRepository = transactionsRepositoryBuilder.Build(),
            MemberScopeResolver = memberScopeResolver
        };
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required OperatorsRepositoryBuilder OperatorsRepositoryBuilder { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; init; }
        public required TransactionsRepositoryBuilder TransactionsRepositoryBuilder { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; init; }
        public required IMemberAccountScopeResolver MemberScopeResolver { get; init; }

        public void StubOperatorInBranch(Operator targetOperator)
        {
            OperatorsRepositoryBuilder
                .GetActiveByIdAndBranchIdAsNoTracking(targetOperator.Id, BranchUser.BranchId, targetOperator);
        }

        public void StubSummary(
            Guid operatorId,
            IReadOnlyList<Guid>? allowedAccountIds,
            OperatorTransactionSummaryProjection projection)
        {
            TransactionsRepositoryBuilder.SumByBranchIdAndOperatorIdAndDateRangeAsNoTrackingReturns(
                BranchUser.BranchId, operatorId, DateFrom, DateTo, allowedAccountIds, projection);
        }
    }
}
