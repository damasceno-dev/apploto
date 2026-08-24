using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.List;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.TimeEntries.List;

public class ListTimeEntriesUseCaseTest
{
    private static readonly DateTime EntryDate = new(2026, 5, 8);
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BranchLocalNow = new(2026, 5, 8, 17, 0, 0);

    [Fact]
    public async Task Execute_ShouldShortCircuitToEmptyPage_ForMemberWithoutLinkedOperator()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: null);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson { Page = 1, PageSize = 20 });

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
        response.Page.ShouldBe(1);
        response.PageSize.ShouldBe(20);
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TimeEntryListFilter>());
        await ctx.TimeEntriesRepository.DidNotReceive().CountByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TimeEntryListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldShortCircuitWithoutRepoCalls_WhenMemberSendsMineAndHasNoLinkedOperator()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: null);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson { Mine = true });

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TimeEntryListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldForceOperatorFilter_ToLinkedOperator_ForMember()
    {
        var linkedOperator = new OperatorBuilder().Build();
        var ctx = BuildContext(Role.Member, linkedOperator: linkedOperator);
        var entry = BuildEntry(linkedOperator);
        ctx.StubListAndCount(filter => filter.OperatorId == linkedOperator.Id, [entry], totalCount: 1);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson { Mine = true });

        response.Items.Count.ShouldBe(1);
        response.Items[0].OperatorId.ShouldBe(linkedOperator.Id);
        await ctx.TimeEntriesRepository.Received(1).ListByBranchIdAsNoTracking(
            ctx.BranchUser.BranchId,
            Arg.Is<TimeEntryListFilter>(filter => filter.OperatorId == linkedOperator.Id));
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberRequestsAnotherOperatorsRows()
    {
        var linkedOperator = new OperatorBuilder().Build();
        var ctx = BuildContext(Role.Member, linkedOperator: linkedOperator);
        var useCase = CreateUseCase(ctx);

        var foreignOperatorId = Guid.NewGuid();
        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(new RequestListTimeEntriesJson { OperatorId = foreignOperatorId }));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
        await ctx.TimeEntriesRepository.DidNotReceive().ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TimeEntryListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldAllowMember_WhenExplicitOperatorIdMatchesLinkedOperator()
    {
        var linkedOperator = new OperatorBuilder().Build();
        var ctx = BuildContext(Role.Member, linkedOperator: linkedOperator);
        var entry = BuildEntry(linkedOperator);
        ctx.StubListAndCount(filter => filter.OperatorId == linkedOperator.Id, [entry], totalCount: 1);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson { OperatorId = linkedOperator.Id });

        response.Items.Count.ShouldBe(1);
        response.Items[0].OperatorId.ShouldBe(linkedOperator.Id);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenMineAndOperatorIdAreCombined()
    {
        var ctx = BuildContext(Role.Admin);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => useCase.Execute(new RequestListTimeEntriesJson { Mine = true, OperatorId = Guid.NewGuid() }));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }

    [Fact]
    public async Task Execute_ShouldComputePagingMetadata_AndOrderingFlowsThroughRepository()
    {
        var ctx = BuildContext(Role.Admin);
        var entries = new[]
        {
            BuildEntry(new OperatorBuilder().WithName("Alice").Build(), date: EntryDate),
            BuildEntry(new OperatorBuilder().WithName("Bob").Build(), date: EntryDate.AddDays(-1))
        };
        ctx.StubListAndCount(_ => true, entries, totalCount: 7);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson { Page = 2, PageSize = 3 });

        response.Items.Count.ShouldBe(2);
        response.TotalCount.ShouldBe(7);
        response.TotalPages.ShouldBe(3);
        response.HasNext.ShouldBeTrue();
        response.HasPrevious.ShouldBeTrue();
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(3);
    }

    [Fact]
    public async Task Execute_ShouldSurfaceHasNextFalseOnLastPage()
    {
        var ctx = BuildContext(Role.Admin);
        ctx.StubListAndCount(_ => true, [BuildEntry()], totalCount: 7);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson { Page = 3, PageSize = 3 });

        response.TotalCount.ShouldBe(7);
        response.TotalPages.ShouldBe(3);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_ShouldRecomputeLiveRunning_WhenAnySegmentIsOpen()
    {
        var ctx = BuildContext(Role.Admin);
        var entry = BuildEntry();
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry)
            .WithClockIn(EntryDate.AddHours(8))
            .WithClockOut(null)
            .Build());
        entry.TotalHours = 0m;
        entry.BalanceHours = 0m;

        ctx.CalculationService.Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>())
            .Returns((9m, 1.67m));
        ctx.StubListAndCount(_ => true, [entry], totalCount: 1);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson());

        response.Items[0].IsInProgress.ShouldBeTrue();
        response.Items[0].TotalHours.ShouldBe(9m);
        response.Items[0].BalanceHours.ShouldBe(1.67m);
        ctx.CalculationService.Received(1).Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            Arg.Any<DateTime>(),
            BranchLocalNow,
            ctx.Policy.DailyTargetHours,
            ctx.Policy.LunchDeductionOver6H,
            ctx.Policy.LunchDeductionOver4H);
    }

    [Fact]
    public async Task Execute_ShouldRecomputeClosedRowsUnderEntryDatePolicy_NotPersistedCheckpoint()
    {
        var ctx = BuildContext(Role.Admin);
        var entry = BuildEntry();
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry)
            .WithClockIn(EntryDate.AddHours(8))
            .WithClockOut(EntryDate.AddHours(17))
            .Build());
        // Deliberately stale checkpoint: a same-day policy change never rewrites persisted
        // totals, so the read path must recompute rather than pass these through.
        entry.TotalHours = 99m;
        entry.BalanceHours = 99m;

        ctx.CalculationService.Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>())
            .Returns((7m, -0.33m));
        ctx.StubListAndCount(_ => true, [entry], totalCount: 1);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(new RequestListTimeEntriesJson());

        response.Items[0].IsInProgress.ShouldBeFalse();
        response.Items[0].TotalHours.ShouldBe(7m);
        response.Items[0].BalanceHours.ShouldBe(-0.33m);
        ctx.CalculationService.Received(1).Calculate(
            entry.Status,
            Arg.Is<IReadOnlyList<TimeEntrySegmentInput>>(list => list.Count == 1 && list[0].ClockOut != null),
            entry.Date,
            BranchLocalNow,
            ctx.Policy.DailyTargetHours,
            ctx.Policy.LunchDeductionOver6H,
            ctx.Policy.LunchDeductionOver4H);
    }

    [Fact]
    public async Task Execute_ShouldRestrictByBranchId_RegardlessOfRequest()
    {
        var ctx = BuildContext(Role.Admin);
        ctx.StubListAndCount(_ => true, [BuildEntry()], totalCount: 1);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(new RequestListTimeEntriesJson());

        await ctx.TimeEntriesRepository.Received(1).ListByBranchIdAsNoTracking(
            ctx.BranchUser.BranchId,
            Arg.Any<TimeEntryListFilter>());
        await ctx.TimeEntriesRepository.Received(1).CountByBranchIdAsNoTracking(
            ctx.BranchUser.BranchId,
            Arg.Any<TimeEntryListFilter>());
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator? LinkedOperator { get; init; }
        public required TimeEntryPolicy Policy { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IMemberAccountScopeResolver MemberScopeResolver { get; init; }
        public required ITimeEntriesRepository TimeEntriesRepository { get; init; }
        public required ITimeEntryPoliciesRepository TimeEntryPoliciesRepository { get; init; }
        public required ITimeEntryCalculationService CalculationService { get; init; }
        public required IBranchClock BranchClock { get; init; }

        public void StubListAndCount(
            Predicate<TimeEntryListFilter> predicate,
            IReadOnlyList<TimeEntry> items,
            int totalCount)
        {
            TimeEntriesRepository
                .ListByBranchIdAsNoTracking(BranchUser.BranchId, Arg.Is<TimeEntryListFilter>(filter => predicate(filter)))
                .Returns(items);
            TimeEntriesRepository
                .CountByBranchIdAsNoTracking(BranchUser.BranchId, Arg.Is<TimeEntryListFilter>(filter => predicate(filter)))
                .Returns(totalCount);
        }
    }

    private static TestContext BuildContext(Role role, Operator? linkedOperator = null)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var policy = new TimeEntryPolicyBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDailyTargetHours(7.33m)
            .WithLunchDeductionOver6H(1m)
            .WithLunchDeductionOver4H(0.25m)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();

        var memberScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
        memberScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId)
            .Returns(new MemberAccountScope(linkedOperator, linkedOperator is null ? [] : [Guid.NewGuid()]));

        var timeEntriesRepository = Substitute.For<ITimeEntriesRepository>();
        var timeEntryPoliciesRepository = new TimeEntryPoliciesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTrackingReturns(branchUser.BranchId, [policy])
            .Build();
        var calculationService = Substitute.For<ITimeEntryCalculationService>();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDateTime(FixedUtcNow).Returns(BranchLocalNow);

        return new TestContext
        {
            BranchUser = branchUser,
            LinkedOperator = linkedOperator,
            Policy = policy,
            AuthenticationService = authenticationService,
            MemberScopeResolver = memberScopeResolver,
            TimeEntriesRepository = timeEntriesRepository,
            TimeEntryPoliciesRepository = timeEntryPoliciesRepository,
            CalculationService = calculationService,
            BranchClock = branchClock
        };
    }

    private static ListTimeEntriesUseCase CreateUseCase(TestContext ctx)
    {
        return new ListTimeEntriesUseCase(
            ctx.AuthenticationService,
            ctx.MemberScopeResolver,
            ctx.TimeEntriesRepository,
            ctx.TimeEntryPoliciesRepository,
            ctx.CalculationService,
            ctx.BranchClock);
    }

    private static TimeEntry BuildEntry(Operator? op = null, DateTime? date = null)
    {
        return new TimeEntryBuilder()
            .WithDate(date ?? EntryDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithTotalHours(8m)
            .WithBalanceHours(0.67m)
            .WithOperator(op ?? new OperatorBuilder().WithName("Lenna").Build())
            .Build();
    }
}
