using CommonTestUtilities.Entities;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.TimeEntries.Upsert;

public class UpsertTimeEntryUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EntryDate = new(2026, 5, 8);

    [Fact]
    public async Task Execute_MemberOpenOnEmptyState_ShouldCreateEntryAndOpenSegmentWithServerClock()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true, action: TimeEntryTapAction.Open);
        ctx.SetLocalNow(EntryDate.AddHours(8));
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Id.ShouldNotBe(Guid.Empty);
        response.IsInProgress.ShouldBeTrue();
        response.Segments.Count.ShouldBe(1);
        response.Segments[0].ClockIn.ShouldBe(EntryDate.AddHours(8));
        response.Segments[0].ClockOut.ShouldBeNull();
        response.UpdatedAt.ShouldBe(FixedUtcNow);
        response.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.StoredEntry.ShouldNotBeNull();
        ctx.StoredEntry.Segments.Single().ClockIn.ShouldBe(EntryDate.AddHours(8));
        await ctx.TimeEntriesRepository.Received(1).Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_MemberCloseWithOpenSegment_ShouldCloseWithServerClock()
    {
        var existing = ExistingEntry(clockIn: EntryDate.AddHours(8), clockOut: null);
        var ctx = BuildContext(Role.Member, linkedOperator: true, action: TimeEntryTapAction.Close, existing: existing);
        ctx.SetLocalNow(EntryDate.AddHours(17));
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Id.ShouldBe(existing.Id);
        response.IsInProgress.ShouldBeFalse();
        response.Segments.Single().ClockOut.ShouldBe(EntryDate.AddHours(17));
        existing.Segments.Single().UpdatedAt.ShouldBe(FixedUtcNow);
        existing.Segments.Single().UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_MemberOpenAfterAllClosed_ShouldAppendNewOpenSegment()
    {
        var existing = ExistingEntry(clockIn: EntryDate.AddHours(8), clockOut: EntryDate.AddHours(12));
        var ctx = BuildContext(Role.Member, linkedOperator: true, action: TimeEntryTapAction.Open, existing: existing);
        ctx.SetLocalNow(EntryDate.AddHours(13));
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Id.ShouldBe(existing.Id);
        response.IsInProgress.ShouldBeTrue();
        response.Segments.Count.ShouldBe(2);
        response.Segments.Last().ClockIn.ShouldBe(EntryDate.AddHours(13));
        response.Segments.Last().ClockOut.ShouldBeNull();
        await ctx.TimeEntriesRepository.DidNotReceive().Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_MemberOpenRetryWhenAlreadyOpen_ShouldReturnNoopWithoutCommit()
    {
        var updatedAt = FixedUtcNow.AddDays(-1);
        var existing = ExistingEntry(clockIn: EntryDate.AddHours(8), clockOut: null);
        existing.UpdatedAt = updatedAt;
        var originalSegmentId = existing.Segments.Single().Id;
        var ctx = BuildContext(Role.Member, linkedOperator: true, action: TimeEntryTapAction.Open, existing: existing);
        ctx.SetLocalNow(EntryDate.AddHours(9));
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Id.ShouldBe(existing.Id);
        response.Segments.Single().Id.ShouldBe(originalSegmentId);
        existing.Segments.Count.ShouldBe(1);
        existing.UpdatedAt.ShouldBe(updatedAt);
        await ctx.TimeEntriesRepository.DidNotReceive().Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_MemberCloseRetryWhenNoEntryExists_ShouldReturnEmptyNoopWithoutCommit()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true, action: TimeEntryTapAction.Close);
        ctx.SetLocalNow(EntryDate.AddHours(17));
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Id.ShouldBe(Guid.Empty);
        response.IsInProgress.ShouldBeFalse();
        response.Segments.ShouldBeEmpty();
        await ctx.TimeEntriesRepository.DidNotReceive().Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_MemberWithSegments_ShouldRejectRoleShape()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder().Build();
        var request = RequestFor(Guid.NewGuid())
            .WithAction(TimeEntryTapAction.Open)
            .WithSegments(segment)
            .Build();
        var ctx = BuildContext(Role.Member, linkedOperator: true, request: request);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MEMBER_SHOULD_NOT_SEND_SEGMENTS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_MemberWithoutActionOrSegments_ShouldRejectRoleShape()
    {
        var request = RequestFor(Guid.NewGuid())
            .WithAction(null)
            .WithSegments((List<RequestTimeEntrySegmentJson>?)null)
            .Build();
        var ctx = BuildContext(Role.Member, linkedOperator: true, request: request);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MEMBER_TAP_ACTION_REQUIRED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminBackfillWithExplicitSegment_ShouldSucceed()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(8))
            .WithClockOut(null)
            .Build();
        var request = RequestFor(Guid.NewGuid()).BuildAdminSnapshot(segment);
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: request);
        ctx.SetLocalNow(EntryDate.AddHours(17));
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.OperatorId.ShouldBe(ctx.TargetOperator.Id);
        response.IsInProgress.ShouldBeTrue();
        response.Segments.Single().ClockIn.ShouldBe(EntryDate.AddHours(8));
        response.Segments.Single().ClockOut.ShouldBeNull();
        response.TotalHours.ShouldBe(8m, tolerance: 0.001m);
        await ctx.TimeEntriesRepository.Received(1).Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_AdminWithSegmentsNull_ShouldRejectRoleShape()
    {
        var request = RequestFor(Guid.NewGuid())
            .WithAction(null)
            .WithSegments((List<RequestTimeEntrySegmentJson>?)null)
            .Build();
        var ctx = BuildContext(Role.Manager, linkedOperator: false, request: request);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_ADMIN_REQUIRES_SEGMENTS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminWithAction_ShouldRejectRoleShape()
    {
        var request = RequestFor(Guid.NewGuid())
            .WithAction(TimeEntryTapAction.Open)
            .WithSegments((List<RequestTimeEntrySegmentJson>?)null)
            .Build();
        var ctx = BuildContext(Role.Manager, linkedOperator: false, request: request);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_ADMIN_SHOULD_NOT_SEND_TAP_ACTION);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminClockInEditAttempt_ShouldReject()
    {
        var existing = ExistingEntry(clockIn: EntryDate.AddHours(8), clockOut: EntryDate.AddHours(12));
        var persistedSegment = existing.Segments.Single();
        var payload = new RequestTimeEntrySegmentJsonBuilder()
            .WithId(persistedSegment.Id)
            .WithClockIn(EntryDate.AddHours(9))
            .WithClockOut(EntryDate.AddHours(12))
            .Build();
        var request = RequestFor(existing.OperatorId).BuildAdminSnapshot(payload);
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: request, existing: existing);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_LOCKED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminMissingExistingSegmentFromPayload_ShouldReject()
    {
        var existing = ExistingEntry(clockIn: EntryDate.AddHours(8), clockOut: EntryDate.AddHours(12));
        var request = RequestFor(existing.OperatorId).BuildAdminSnapshot();
        var ctx = BuildContext(Role.Manager, linkedOperator: false, request: request, existing: existing);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminStatusChangeToVacationWithActiveSegments_ShouldConflict()
    {
        var existing = ExistingEntry(clockIn: EntryDate.AddHours(8), clockOut: EntryDate.AddHours(12));
        var request = RequestFor(existing.OperatorId)
            .WithStatus(TimeEntryStatus.Vacation)
            .BuildAdminSnapshot();
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: request, existing: existing);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_STATUS_CHANGE_REQUIRES_SEGMENT_CLEANUP);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminOverlappingSegments_ShouldReject()
    {
        var first = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(8))
            .WithClockOut(EntryDate.AddHours(12))
            .Build();
        var second = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(11))
            .WithClockOut(EntryDate.AddHours(17))
            .Build();
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: RequestFor(Guid.NewGuid()).BuildAdminSnapshot(first, second));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminOutOfDayBoundsSegment_ShouldReject()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddDays(1))
            .WithClockOut(EntryDate.AddDays(1).AddHours(2))
            .Build();
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: RequestFor(Guid.NewGuid()).BuildAdminSnapshot(segment));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminMultipleOpenSegments_ShouldReject()
    {
        var first = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(8))
            .WithClockOut(null)
            .Build();
        var second = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(13))
            .WithClockOut(null)
            .Build();
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: RequestFor(Guid.NewGuid()).BuildAdminSnapshot(first, second));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AdminClockOutBeforeClockIn_ShouldReject()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(12))
            .WithClockOut(EntryDate.AddHours(11))
            .Build();
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: RequestFor(Guid.NewGuid()).BuildAdminSnapshot(segment));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_MemberFourTapE2E_ShouldKeepSameEntryAndStableSegmentIds()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true, action: TimeEntryTapAction.Open);
        var useCase = CreateUseCase(ctx);

        ctx.SetLocalNow(EntryDate.AddHours(8));
        var open1 = await useCase.Execute(ctx.Request);
        var firstSegmentId = open1.Segments.Single().Id;

        ctx.Request = RequestFor(ctx.TargetOperator.Id).BuildMemberCloseTap();
        ctx.SetLocalNow(EntryDate.AddHours(12));
        var close1 = await useCase.Execute(ctx.Request);

        ctx.Request = RequestFor(ctx.TargetOperator.Id).BuildMemberOpenTap();
        ctx.SetLocalNow(EntryDate.AddHours(13));
        var open2 = await useCase.Execute(ctx.Request);
        var secondSegmentId = open2.Segments.Last().Id;

        ctx.Request = RequestFor(ctx.TargetOperator.Id).BuildMemberCloseTap();
        ctx.SetLocalNow(EntryDate.AddHours(17));
        var close2 = await useCase.Execute(ctx.Request);

        close1.Id.ShouldBe(open1.Id);
        open2.Id.ShouldBe(open1.Id);
        close2.Id.ShouldBe(open1.Id);
        close1.Segments.Single().Id.ShouldBe(firstSegmentId);
        close2.Segments[0].Id.ShouldBe(firstSegmentId);
        close2.Segments[1].Id.ShouldBe(secondSegmentId);
        open1.IsInProgress.ShouldBeTrue();
        close1.IsInProgress.ShouldBeFalse();
        open2.IsInProgress.ShouldBeTrue();
        close2.IsInProgress.ShouldBeFalse();
        close2.TotalHours.ShouldBe(8m, tolerance: 0.001m);
        close2.BalanceHours.ShouldBe(8m - ctx.Setting.DailyTargetHours, tolerance: 0.001m);
        await ctx.UnitOfWork.Received(4).Commit();
    }

    [Fact]
    public async Task Execute_MemberRetryE2E_ShouldNotCreatePhantomSegments()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true, action: TimeEntryTapAction.Open);
        var useCase = CreateUseCase(ctx);

        ctx.SetLocalNow(EntryDate.AddHours(8));
        var open1 = await useCase.Execute(ctx.Request);

        ctx.SetLocalNow(EntryDate.AddHours(8.5));
        var openRetry = await useCase.Execute(ctx.Request);

        ctx.Request = RequestFor(ctx.TargetOperator.Id).BuildMemberCloseTap();
        ctx.SetLocalNow(EntryDate.AddHours(17));
        var close1 = await useCase.Execute(ctx.Request);

        ctx.SetLocalNow(EntryDate.AddHours(17.5));
        var closeRetry = await useCase.Execute(ctx.Request);

        openRetry.Id.ShouldBe(open1.Id);
        openRetry.Segments.Count.ShouldBe(1);
        closeRetry.Id.ShouldBe(open1.Id);
        closeRetry.Segments.Count.ShouldBe(1);
        closeRetry.Segments.Single().ClockIn.ShouldBe(EntryDate.AddHours(8));
        closeRetry.Segments.Single().ClockOut.ShouldBe(EntryDate.AddHours(17));
        closeRetry.IsInProgress.ShouldBeFalse();
        ctx.StoredEntry.ShouldNotBeNull();
        ctx.StoredEntry.Segments.Count.ShouldBe(1);
        await ctx.UnitOfWork.Received(2).Commit();
    }

    [Fact]
    public async Task Execute_AdminSingleSegmentOvernight_ShouldSucceedAndCalculateWithDateTime()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(new DateTime(2026, 5, 8, 22, 0, 0))
            .WithClockOut(new DateTime(2026, 5, 9, 6, 0, 0))
            .Build();
        var request = RequestFor(Guid.NewGuid()).BuildAdminSnapshot(segment);
        var ctx = BuildContext(Role.Admin, linkedOperator: false, request: request);
        ctx.SetLocalNow(new DateTime(2026, 5, 9, 6, 0, 0));
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Segments.Single().ClockOut.ShouldBe(new DateTime(2026, 5, 9, 6, 0, 0));
        response.TotalHours.ShouldBe(7m, tolerance: 0.001m);
        response.BalanceHours.ShouldBe(7m - ctx.Setting.DailyTargetHours, tolerance: 0.001m);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_MemberPriorDayOpen_ShouldUseSubmittedActionAndDateToDisambiguateOvernightVsForgottenClose()
    {
        var priorDate = EntryDate;
        var today = EntryDate.AddDays(1);
        var operatorId = Guid.NewGuid();
        var closePriorDayRequest = RequestFor(operatorId)
            .WithDate(priorDate)
            .BuildMemberCloseTap();
        var overnightEntry = ExistingEntry(
            clockIn: priorDate.AddHours(22),
            clockOut: null,
            date: priorDate,
            operatorId: operatorId);
        var closeCtx = BuildContext(
            Role.Member,
            linkedOperator: true,
            request: closePriorDayRequest,
            existing: overnightEntry);
        closeCtx.BranchClock.IsSameLocalDay(priorDate, Arg.Any<DateTime>()).Returns(false);
        closeCtx.SetLocalNow(today.AddHours(8));
        var closeUseCase = CreateUseCase(closeCtx);

        var closedOvernight = await closeUseCase.Execute(closeCtx.Request);

        closedOvernight.Id.ShouldBe(overnightEntry.Id);
        closedOvernight.IsInProgress.ShouldBeFalse();
        closedOvernight.Segments.Single().ClockIn.ShouldBe(priorDate.AddHours(22));
        closedOvernight.Segments.Single().ClockOut.ShouldBe(today.AddHours(8));
        await closeCtx.UnitOfWork.Received(1).Commit();

        var forgottenPriorEntry = ExistingEntry(
            clockIn: priorDate.AddHours(22),
            clockOut: null,
            date: priorDate,
            operatorId: operatorId);
        var openTodayRequest = RequestFor(operatorId)
            .WithDate(today)
            .BuildMemberOpenTap();
        var openCtx = BuildContext(Role.Member, linkedOperator: true, request: openTodayRequest);
        openCtx.SetLocalNow(today.AddHours(8));
        var openUseCase = CreateUseCase(openCtx);

        var openedToday = await openUseCase.Execute(openCtx.Request);

        openedToday.Date.ShouldBe(today);
        openedToday.IsInProgress.ShouldBeTrue();
        openedToday.Segments.Single().ClockIn.ShouldBe(today.AddHours(8));
        forgottenPriorEntry.Segments.Single().ClockOut.ShouldBeNull();
        await openCtx.TimeEntriesRepository.Received(1)
            .GetByBranchIdOperatorIdAndDate(openCtx.BranchUser.BranchId, operatorId, today);
        await openCtx.TimeEntriesRepository.DidNotReceive()
            .GetByBranchIdOperatorIdAndDate(openCtx.BranchUser.BranchId, operatorId, priorDate);
        await openCtx.UnitOfWork.Received(1).Commit();
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator? CallerOperator { get; init; }
        public required Operator TargetOperator { get; set; }
        public required RequestUpsertTimeEntryJson Request { get; set; }
        public required Setting Setting { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; init; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; init; }
        public required ITimeEntriesRepository TimeEntriesRepository { get; init; }
        public required ITimeEntrySegmentsRepository TimeEntrySegmentsRepository { get; init; }
        public required ISettingsRepository SettingsRepository { get; init; }
        public required IHolidaysRepository HolidaysRepository { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
        public required Action<DateTime> SetLocalNow { get; init; }
        public TimeEntry? StoredEntry { get; set; }
    }

    private static TestContext BuildContext(
        Role role,
        bool linkedOperator,
        TimeEntryTapAction? action = null,
        RequestUpsertTimeEntryJson? request = null,
        TimeEntry? existing = null)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var requestedOperatorId = request?.OperatorId ?? existing?.OperatorId ?? Guid.NewGuid();
        var callerOperator = linkedOperator
            ? new OperatorBuilder()
                .WithId(requestedOperatorId)
                .WithBranchId(branchUser.BranchId)
                .WithUserId(branchUser.UserId)
                .Build()
            : null;
        var targetOperator = callerOperator ?? new OperatorBuilder()
            .WithId(requestedOperatorId)
            .WithBranchId(branchUser.BranchId)
            .WithName("Lenna Doe")
            .Build();
        request ??= action is TimeEntryTapAction.Close
            ? RequestFor(targetOperator.Id).BuildMemberCloseTap()
            : action is TimeEntryTapAction.Open
                ? RequestFor(targetOperator.Id).BuildMemberOpenTap()
                : RequestFor(targetOperator.Id).BuildAdminSnapshot();

        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            BranchId = branchUser.BranchId,
            DailyTargetHours = 7.33m,
            LunchDeductionOver6H = 1m,
            LunchDeductionOver4H = 0.25m
        };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();

        var operatorsRepository = Substitute.For<IOperatorsRepository>();
        operatorsRepository
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId)
            .Returns(callerOperator);
        operatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(request.OperatorId, branchUser.BranchId)
            .Returns(targetOperator);

        var operatorAccountsRepository = Substitute.For<IOperatorAccountsRepository>();
        operatorAccountsRepository
            .ListActiveByOperatorIdAsNoTracking(Arg.Any<Guid>())
            .Returns([]);

        var timeEntriesRepository = Substitute.For<ITimeEntriesRepository>();
        var timeEntrySegmentsRepository = Substitute.For<ITimeEntrySegmentsRepository>();
        var settingsRepository = Substitute.For<ISettingsRepository>();
        settingsRepository.GetByBranchIdAsNoTracking(branchUser.BranchId).Returns(setting);
        var holidaysRepository = Substitute.For<IHolidaysRepository>();

        var branchClock = Substitute.For<IBranchClock>();
        var currentLocalNow = EntryDate.AddHours(18);
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDateTime(Arg.Any<DateTime>()).Returns(_ => currentLocalNow);
        branchClock.IsSameLocalDay(Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(true);

        var unitOfWork = new UnitOfWorkBuilder().Build();

        var ctx = new TestContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            TargetOperator = targetOperator,
            Request = request,
            Setting = setting,
            AuthenticationService = authenticationService,
            OperatorsRepository = operatorsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            TimeEntriesRepository = timeEntriesRepository,
            TimeEntrySegmentsRepository = timeEntrySegmentsRepository,
            SettingsRepository = settingsRepository,
            HolidaysRepository = holidaysRepository,
            BranchClock = branchClock,
            UnitOfWork = unitOfWork,
            SetLocalNow = value => currentLocalNow = value,
            StoredEntry = existing
        };

        timeEntriesRepository
            .GetByBranchIdOperatorIdAndDate(branchUser.BranchId, request.OperatorId, request.Date)
            .Returns(_ => ctx.StoredEntry);
        timeEntriesRepository
            .Add(Arg.Do<TimeEntry>(entry => ctx.StoredEntry = entry))
            .Returns(Task.CompletedTask);
        timeEntrySegmentsRepository
            .Add(Arg.Any<TimeEntrySegment>())
            .Returns(Task.CompletedTask);

        return ctx;
    }

    private static UpsertTimeEntryUseCase CreateUseCase(TestContext ctx)
    {
        return new UpsertTimeEntryUseCase(
            ctx.AuthenticationService,
            new MemberAccountScopeResolver(ctx.OperatorsRepository, ctx.OperatorAccountsRepository),
            ctx.OperatorsRepository,
            ctx.TimeEntriesRepository,
            ctx.TimeEntrySegmentsRepository,
            ctx.SettingsRepository,
            ctx.HolidaysRepository,
            new TimeEntryWritePermissionGuard(ctx.BranchClock),
            new TimeEntryCalculationService(),
            ctx.BranchClock,
            ctx.UnitOfWork);
    }

    private static RequestUpsertTimeEntryJsonBuilder RequestFor(Guid operatorId)
    {
        return new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(operatorId)
            .WithDate(EntryDate)
            .WithStatus(TimeEntryStatus.Present);
    }

    private static TimeEntry ExistingEntry(
        DateTime clockIn,
        DateTime? clockOut,
        DateTime? date = null,
        Guid? branchId = null,
        Guid? operatorId = null)
    {
        var entry = new TimeEntryBuilder()
            .WithBranchId(branchId ?? Guid.NewGuid())
            .WithOperatorId(operatorId ?? Guid.NewGuid())
            .WithDate(date ?? EntryDate)
            .WithStatus(TimeEntryStatus.Present)
            .Build();

        entry.Segments.Add(new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            CreatedAt = FixedUtcNow.AddHours(-1),
            TimeEntryId = entry.Id,
            ClockIn = clockIn,
            ClockOut = clockOut
        });

        return entry;
    }
}
