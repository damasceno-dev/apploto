using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.DailyCloses.Reject;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.DailyCloses.Reject;

public class RejectDailyCloseUseCaseTest
{
    public static TheoryData<Role> ReviewerRoles =>
    [
        Role.Manager,
        Role.Admin
    ];

    public static TheoryData<DailyCloseStatus> NotRejectableStatuses =>
    [
        DailyCloseStatus.Draft,
        DailyCloseStatus.Rejected,
        DailyCloseStatus.Approved
    ];

    [Theory]
    [MemberData(nameof(ReviewerRoles))]
    public async Task Execute_ShouldRejectSubmittedCloseAndStampSingleInstantAudit_WhenManagerOrAdmin(Role role)
    {
        var ctx = BuildContext(role);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        response.Status.ShouldBe(DailyCloseStatus.Rejected);
        response.RejectionReason.ShouldBe(ctx.Request.RejectionReason);
        response.UpdatedAt.ShouldBe(ctx.Now);
        response.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        response.ApprovedAt.ShouldBeNull();
        response.ApprovedByUserId.ShouldBeNull();

        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Rejected);
        ctx.DailyClose.RejectionReason.ShouldBe(ctx.Request.RejectionReason);
        ctx.DailyClose.UpdatedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.DailyClose.ApprovedAt.ShouldBeNull();
        ctx.DailyClose.ApprovedByUserId.ShouldBeNull();
        ctx.DailyClose.ApprovedByUser.ShouldBeNull();

        ctx.WorkflowGuard.Received(1).EnsureCanReject(ctx.DailyClose, ctx.BranchUser);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDefensivelyClearApprovedFields_WhenRejectingInMemoryShapeHasApprovedValues()
    {
        var ctx = BuildContext(Role.Manager);
        var approvedByUser = new UserBuilder().Build();
        ctx.DailyClose = new DailyCloseBuilder()
            .WithId(ctx.DailyClose.Id)
            .WithStatus(DailyCloseStatus.Submitted)
            .WithAccount(ctx.DailyClose.Account)
            .WithDate(ctx.DailyClose.Date)
            .WithSubmittedAt(ctx.DailyClose.SubmittedAt)
            .WithApprovedByUser(approvedByUser)
            .WithApprovedAt(ctx.Now.AddMinutes(-5))
            .Build();
        RewireDailyCloseRepository(ctx);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        ctx.DailyClose.ApprovedAt.ShouldBeNull();
        ctx.DailyClose.ApprovedByUserId.ShouldBeNull();
        ctx.DailyClose.ApprovedByUser.ShouldBeNull();
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenDailyCloseIsMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        ctx.WorkflowGuard.DidNotReceiveWithAnyArgs().EnsureCanReject(null!, null!);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Theory]
    [MemberData(nameof(NotRejectableStatuses))]
    public async Task Execute_ShouldThrowConflict_WhenCloseIsNotSubmitted(DailyCloseStatus status)
    {
        var ctx = BuildContext(Role.Manager, status);
        ctx.WorkflowGuard = new DailyCloseWorkflowGuard(ctx.BranchClock);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_REJECTABLE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberRejects()
    {
        var ctx = BuildContext(Role.Member);
        ctx.WorkflowGuard = new DailyCloseWorkflowGuard(ctx.BranchClock);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

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

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        ctx.WorkflowGuard.Received(1).EnsureCanReject(ctx.DailyClose, ctx.BranchUser);
        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Submitted);
        ctx.DailyClose.RejectionReason.ShouldBeNull();
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private static RejectDailyCloseUseCase CreateUseCase(TestContext ctx)
    {
        var lockDateGuard = new LockDateGuard(ctx.SettingsRepository);

        return new RejectDailyCloseUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            ctx.WorkflowGuard,
            lockDateGuard,
            ctx.BranchClock,
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

        return new TestContext
        {
            Now = now,
            BranchUser = branchUser,
            DailyClose = close,
            Request = new RequestRejectDailyCloseJsonBuilder()
                .WithRejectionReason("test")
                .Build(),
            AuthenticationService = authenticationService,
            DailyClosesRepository = dailyClosesRepository,
            SettingsRepository = settingsRepository,
            WorkflowGuard = Substitute.For<IDailyCloseWorkflowGuard>(),
            BranchClock = new FixedBranchClock(now),
            UnitOfWork = new UnitOfWorkBuilder().Build()
        };
    }

    private static void RewireDailyCloseRepository(TestContext ctx)
    {
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, ctx.DailyClose)
            .Build();
    }

    private sealed class FixedBranchClock(DateTime now) : IBranchClock
    {
        public DateTime UtcNow() => now;
        public DateTime LocalBusinessDate(DateTime utcInstant) => utcInstant.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
            => localBusinessDate.Date == LocalBusinessDate(utcInstant);
    }

    private sealed class TestContext
    {
        public required DateTime Now { get; init; }
        public required BranchUser BranchUser { get; init; }
        public required DailyClose DailyClose { get; set; }
        public required RequestRejectDailyCloseJson Request { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IDailyCloseWorkflowGuard WorkflowGuard { get; set; }
        public required IBranchClock BranchClock { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }
}
