using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.DailyCloses.Recall;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.DailyCloses.Recall;

public class RecallDailyCloseUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldRecallSubmittedCloseAndPreserveItems_WhenRecordingMemberOnSameDay()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Close.Id);

        response.Status.ShouldBe(DailyCloseStatus.Draft);
        ctx.Close.Status.ShouldBe(DailyCloseStatus.Draft);
        ctx.Close.SubmittedAt.ShouldBeNull();
        ctx.Close.RejectionReason.ShouldBeNull();
        ctx.Close.UpdatedAt.ShouldBe(ctx.Now);
        ctx.Close.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.Close.Items.ShouldHaveSingleItem().ShouldBeSameAs(ctx.PersistedItem);
        ctx.PersistedItem.Value.ShouldBe(37.50m);
        await ctx.Coordination.Received(1).Acquire(
            ctx.BranchUser.BranchId,
            ctx.Close.AccountId,
            Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.Received(1).Commit();
        await ctx.CoordinationScope.Received(1).Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldRejectRecordingMemberRecallOnLaterDay()
    {
        var ctx = BuildContext(Role.Member, date: new DateTime(2026, 4, 29));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.Close.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_RECALL_REQUIRES_SAME_DAY);
        ctx.Close.Status.ShouldBe(DailyCloseStatus.Submitted);
        await ctx.UnitOfWork.DidNotReceive().Commit();
        await ctx.CoordinationScope.DidNotReceive().Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldRespectPeriodLock()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting { BranchId = ctx.BranchUser.BranchId, LockDate = ctx.Close.Date })
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Close.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        ctx.Close.Status.ShouldBe(DailyCloseStatus.Submitted);
        await ctx.UnitOfWork.DidNotReceive().Commit();
        await ctx.CoordinationScope.DidNotReceive().Complete(Arg.Any<CancellationToken>());
    }

    private static RecallDailyCloseUseCase CreateUseCase(TestContext ctx)
    {
        return new RecallDailyCloseUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            ctx.MemberAccountScopeResolver,
            new MemberAccountScopeGuard(),
            new DailyCloseWorkflowGuard(ctx.BranchClock),
            new DailyCloseDraftTransition(),
            new LockDateGuard(new LockDateReader(ctx.SettingsRepository)),
            ctx.BranchClock,
            new CashVarianceProductResolverBuilder()
                .ReturnsId(ctx.BranchUser.BranchId, Guid.NewGuid())
                .Build(),
            ctx.Coordination,
            ctx.UnitOfWork);
    }

    private static TestContext BuildContext(Role role, DateTime? date = null)
    {
        var now = new DateTime(2026, 4, 30, 14, 30, 0, DateTimeKind.Utc);
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var item = new DailyCloseItemBuilder().WithValue(37.50m).Build();
        var close = new DailyCloseBuilder()
            .WithStatus(DailyCloseStatus.Submitted)
            .WithAccount(account)
            .WithDate(date ?? now.Date)
            .WithRecordedBy(branchUser.User, callerOperator)
            .WithItemsFirstRecordedAt(now.AddHours(-2))
            .WithSubmittedBy(branchUser.User, callerOperator)
            .WithSubmittedAt(now.AddHours(-1))
            .WithRejectionReason("legacy marker")
            .WithItems([item])
            .Build();
        var memberAccountScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
        memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId)
            .Returns(new MemberAccountScope(callerOperator, [account.Id]));
        var coordinationBuilder = new DailyCloseAccountCoordinationBuilder();

        return new TestContext
        {
            Now = now,
            BranchUser = branchUser,
            Close = close,
            PersistedItem = item,
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            DailyClosesRepository = new DailyClosesRepositoryBuilder()
                .GetByIdAndBranchIdReturns(close.Id, branchUser.BranchId, close)
                .Build(),
            MemberAccountScopeResolver = memberAccountScopeResolver,
            SettingsRepository = new SettingsRepositoryBuilder()
                .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
                .Build(),
            BranchClock = new FixedBranchClock(now),
            Coordination = coordinationBuilder.Build(),
            CoordinationScope = coordinationBuilder.Scope,
            UnitOfWork = new UnitOfWorkBuilder().Build()
        };
    }

    private sealed class FixedBranchClock(DateTime now) : IBranchClock
    {
        public DateTime UtcNow() => now;
        public DateTime LocalBusinessDateTime(DateTime utcInstant) => utcInstant;
        public DateTime LocalBusinessDate(DateTime utcInstant) => utcInstant.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
            => localBusinessDate.Date == utcInstant.Date;
    }

    private sealed class TestContext
    {
        public required DateTime Now { get; init; }
        public required BranchUser BranchUser { get; init; }
        public required DailyClose Close { get; init; }
        public required DailyCloseItem PersistedItem { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; init; }
        public required IMemberAccountScopeResolver MemberAccountScopeResolver { get; init; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IBranchClock BranchClock { get; init; }
        public required IDailyCloseAccountCoordination Coordination { get; init; }
        public required IDailyCloseAccountCoordinationScope CoordinationScope { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }
}
