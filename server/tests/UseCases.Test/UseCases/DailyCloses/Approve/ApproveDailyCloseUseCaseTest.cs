using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.DailyCloses.Approve;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.DailyCloses.Approve;

public class ApproveDailyCloseUseCaseTest
{
    public static TheoryData<Role> ApproverRoles =>
    [
        Role.Manager,
        Role.Admin
    ];

    public static TheoryData<DailyCloseStatus> NotApprovableStatuses =>
    [
        DailyCloseStatus.Draft,
        DailyCloseStatus.Approved,
        DailyCloseStatus.Rejected
    ];

    [Theory]
    [MemberData(nameof(ApproverRoles))]
    public async Task Execute_ShouldApproveSubmittedCloseAndStampSingleInstantAudit_WhenManagerOrAdmin(Role role)
    {
        var ctx = BuildContext(role);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.DailyClose.Id);

        response.Status.ShouldBe(DailyCloseStatus.Approved);
        response.ApprovedAt.ShouldBe(ctx.Now);
        response.UpdatedAt.ShouldBe(ctx.Now);
        response.ApprovedAt.ShouldBe(response.UpdatedAt);
        response.ApprovedByUserId.ShouldBe(ctx.BranchUser.UserId);
        response.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Approved);
        ctx.DailyClose.ApprovedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.UpdatedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.ApprovedAt.ShouldBe(ctx.DailyClose.UpdatedAt);
        ctx.DailyClose.ApprovedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.DailyClose.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        ctx.WorkflowGuard.Received(1).EnsureCanApprove(ctx.DailyClose, ctx.BranchUser);
        await ctx.DailyCloseLedgerCoordination.Received(1).Acquire(
            ctx.BranchUser.BranchId,
            ctx.DailyClose.AccountId,
            Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.Received(1).Commit();
        await ctx.DailyCloseLedgerCoordinationScope.Received(1).Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenDailyCloseIsMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        ctx.WorkflowGuard.DidNotReceiveWithAnyArgs().EnsureCanApprove(null!, null!);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Theory]
    [MemberData(nameof(NotApprovableStatuses))]
    public async Task Execute_ShouldThrowConflict_WhenCloseIsNotSubmitted(DailyCloseStatus status)
    {
        var ctx = BuildContext(Role.Manager, status);
        ctx.WorkflowGuard = new DailyCloseWorkflowGuard(ctx.BranchClock);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_APPROVABLE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberApproves()
    {
        var ctx = BuildContext(Role.Member);
        ctx.WorkflowGuard = new DailyCloseWorkflowGuard(ctx.BranchClock);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksCloseDate()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting
                {
                    BranchId = ctx.BranchUser.BranchId,
                    LockDate = ctx.DailyClose.Date
                })
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        ctx.WorkflowGuard.Received(1).EnsureCanApprove(ctx.DailyClose, ctx.BranchUser);
        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Submitted);
        ctx.DailyClose.ApprovedAt.ShouldBeNull();
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private static ApproveDailyCloseUseCase CreateUseCase(TestContext ctx)
    {
        var lockDateGuard = new LockDateGuard(new LockDateReader(ctx.SettingsRepository));

        return new ApproveDailyCloseUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            ctx.WorkflowGuard,
            lockDateGuard,
            ctx.BranchClock,
            new CashVarianceProductResolverBuilder()
                .ReturnsId(ctx.BranchUser.BranchId, Guid.NewGuid())
                .Build(),
            ctx.DailyCloseLedgerCoordination,
            ctx.UnitOfWork);
    }

    private static TestContext BuildContext(
        Role role,
        DailyCloseStatus status = DailyCloseStatus.Submitted)
    {
        var now = new DateTime(2026, 4, 30, 14, 30, 0, DateTimeKind.Utc);
        var branchUser = new BranchUserBuilder()
            .WithRole(role)
            .Build();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var close = new DailyCloseBuilder()
            .WithStatus(status)
            .WithAccount(account)
            .WithDate(now.Date)
            .WithSubmittedAt(now.AddHours(-1))
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(close.Id, branchUser.BranchId, close)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
            .Build();
        var coordinationBuilder = new DailyCloseAccountCoordinationBuilder();

        return new TestContext
        {
            Now = now,
            BranchUser = branchUser,
            DailyClose = close,
            AuthenticationService = authenticationService,
            DailyClosesRepository = dailyClosesRepository,
            SettingsRepository = settingsRepository,
            WorkflowGuard = Substitute.For<IDailyCloseWorkflowGuard>(),
            BranchClock = new FixedBranchClock(now),
            DailyCloseLedgerCoordination = coordinationBuilder.Build(),
            DailyCloseLedgerCoordinationScope = coordinationBuilder.Scope,
            UnitOfWork = new UnitOfWorkBuilder().Build()
        };
    }

    private sealed class FixedBranchClock(DateTime now) : IBranchClock
    {
        public DateTime UtcNow() => now;
        public DateTime LocalBusinessDateTime(DateTime utcInstant) => utcInstant;
        public DateTime LocalBusinessDate(DateTime utcInstant) => utcInstant.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
            => localBusinessDate.Date == LocalBusinessDate(utcInstant);
    }

    private sealed class TestContext
    {
        public required DateTime Now { get; init; }
        public required BranchUser BranchUser { get; init; }
        public required DailyClose DailyClose { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IDailyCloseWorkflowGuard WorkflowGuard { get; set; }
        public required IBranchClock BranchClock { get; init; }
        public required IDailyCloseAccountCoordination DailyCloseLedgerCoordination { get; init; }
        public required IDailyCloseAccountCoordinationScope DailyCloseLedgerCoordinationScope { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }
}
