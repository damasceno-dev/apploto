using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Security;
using server.Application.Services.Sessions;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Sessions.Approve;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.Sessions.Approve;

public class ApproveSessionUseCaseTest
{
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public async Task Execute_ShouldApprove_WhenApproverIsIndependent(Role role)
    {
        var ctx = new Context(role);
        var useCase = ctx.CreateUseCase();

        var response = await useCase.Execute(ctx.Session.Id, new RequestApproveSessionJson(), ctx.Session.Version, "key-1");

        response.Status.ShouldBe(DailyCloseStatus.Approved);
        ctx.Session.Status.ShouldBe(DailyCloseStatus.Approved);
        ctx.Session.ApprovedByUserId.ShouldBe(ctx.Approver.UserId);
        ctx.Session.ApprovedAt.ShouldBe(ctx.Now);
        ctx.Session.CustodyState.ShouldBe(SessionCustodyState.Finished);
        await ctx.Events.Received(1).Add(
            Arg.Is<SessionEvent>(sessionEvent => sessionEvent.Kind == SessionEventKind.SessionApproved && sessionEvent.SessionId == ctx.Session.Id),
            Arg.Any<CancellationToken>());
        await ctx.BranchUsers.DidNotReceiveWithAnyArgs().CountActiveManagersByBranchIdAsNoTracking(Guid.Empty, CancellationToken.None);
        await ctx.Security.Received(1).HoldSharedInCurrentTransaction(SecurityRules.BranchKey(ctx.Approver.BranchId), Arg.Any<CancellationToken>());
        await ctx.BranchUsers.Received(1).LockMembershipsByBranchId(ctx.Approver.BranchId, Arg.Any<CancellationToken>());
        await ctx.BranchUsers.Received(1).GetActiveById(ctx.Approver.Id);
        await ctx.Scope.Received(1).Complete(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SessionApprovalMode.Independent)]
    [InlineData(SessionApprovalMode.SingleActiveManager)]
    public async Task Execute_ShouldRefuse_WhenApproverWasDemotedBeforeTheBranchLock(SessionApprovalMode mode)
    {
        var ctx = new Context(Role.Manager);
        ctx.BranchUsers.GetActiveById(ctx.Approver.Id)
            .Returns(new BranchUserBuilder().WithId(ctx.Approver.Id).WithUser(ctx.Approver.User!).WithBranchId(ctx.Approver.BranchId).WithRole(Role.Member).Build());
        ctx.BranchUsers.CountActiveManagersByBranchIdAsNoTracking(ctx.Approver.BranchId, Arg.Any<CancellationToken>()).Returns(1);
        var useCase = ctx.CreateUseCase();

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(
            ctx.Session.Id,
            new RequestApproveSessionJson { Mode = mode, Reason = "Sozinha hoje" },
            ctx.Session.Version,
            "key-1"));

        ctx.Session.Status.ShouldBe(DailyCloseStatus.Submitted);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRefuse_WhenApproverMembershipWasRemovedBeforeTheBranchLock()
    {
        var ctx = new Context(Role.Manager);
        ctx.BranchUsers.GetActiveById(ctx.Approver.Id).Returns((BranchUser?)null);
        var useCase = ctx.CreateUseCase();

        await Should.ThrowAsync<TokenInvalidException>(() => useCase.Execute(
            ctx.Session.Id, new RequestApproveSessionJson(), ctx.Session.Version, "key-1"));

        ctx.Session.Status.ShouldBe(DailyCloseStatus.Submitted);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRefuse_WhenApproverRecordedTheCount()
    {
        var ctx = new Context(Role.Manager);
        ctx.Session.RecordedByUserId = ctx.Approver.UserId;

        await ShouldRefuse(ctx, ResourcesErrorMessages.SESSION_APPROVER_IS_RECORDER);
    }

    [Fact]
    public async Task Execute_ShouldRefuse_WhenApproverOperatorRecordedTheCount()
    {
        var ctx = new Context(Role.Manager, approverHasOperator: true);
        ctx.Session.RecordedByOperatorId = ctx.ApproverOperator!.Id;

        await ShouldRefuse(ctx, ResourcesErrorMessages.SESSION_APPROVER_IS_RECORDER);
    }

    [Fact]
    public async Task Execute_ShouldRefuse_WhenApproverSubmitted()
    {
        var ctx = new Context(Role.Manager);
        ctx.Session.SubmittedByUserId = ctx.Approver.UserId;

        await ShouldRefuse(ctx, ResourcesErrorMessages.SESSION_APPROVER_IS_SUBMITTER);
    }

    [Fact]
    public async Task Execute_ShouldRefuse_WhenApproverWasTheCustodianAtStart()
    {
        var ctx = new Context(Role.Manager);
        ctx.Session = ctx.Rebuild(custodianUserIdAtStart: ctx.Approver.UserId);

        await ShouldRefuse(ctx, ResourcesErrorMessages.SESSION_APPROVER_IS_CUSTODIAN);
    }

    [Fact]
    public async Task Execute_ShouldRefuse_WhenTheCustodianOperatorIsNowLinkedToTheApprover()
    {
        var ctx = new Context(Role.Manager);
        ctx.Custodian.UserId = ctx.Approver.UserId;

        await ShouldRefuse(ctx, ResourcesErrorMessages.SESSION_APPROVER_IS_CUSTODIAN);
    }

    [Fact]
    public async Task Execute_ShouldApproveAsSingleManager_WithReasonAndMembershipCountUnderBranchLock()
    {
        var ctx = new Context(Role.Manager);
        ctx.Session.SubmittedByUserId = ctx.Approver.UserId;
        ctx.BranchUsers.CountActiveManagersByBranchIdAsNoTracking(ctx.Approver.BranchId, Arg.Any<CancellationToken>()).Returns(1);
        var useCase = ctx.CreateUseCase();

        await useCase.Execute(
            ctx.Session.Id,
            new RequestApproveSessionJson { Mode = SessionApprovalMode.SingleActiveManager, Reason = "  Única gestora  " },
            ctx.Session.Version,
            "key-1");

        ctx.Session.Status.ShouldBe(DailyCloseStatus.Approved);
        await ctx.BranchUsers.Received(1).CountActiveManagersByBranchIdAsNoTracking(ctx.Approver.BranchId, Arg.Any<CancellationToken>());
        await ctx.Events.Received(1).Add(
            Arg.Is<SessionEvent>(sessionEvent =>
                sessionEvent.Kind == SessionEventKind.SessionApproved
                && sessionEvent.Reason == "Única gestora"
                && sessionEvent.Details.Contains("\"activeManagerCount\":1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldRefuseSingleManagerApproval_WhenAnotherActiveManagerExists()
    {
        var ctx = new Context(Role.Manager);
        ctx.BranchUsers.CountActiveManagersByBranchIdAsNoTracking(ctx.Approver.BranchId, Arg.Any<CancellationToken>()).Returns(2);
        var useCase = ctx.CreateUseCase();

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(
            ctx.Session.Id,
            new RequestApproveSessionJson { Mode = SessionApprovalMode.SingleActiveManager, Reason = "Sozinha hoje" },
            ctx.Session.Version,
            "key-1"));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_SINGLE_MANAGER_APPROVAL_NOT_AVAILABLE);
        ctx.Session.Status.ShouldBe(DailyCloseStatus.Submitted);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRequireReason_ForSingleManagerApproval()
    {
        var ctx = new Context(Role.Manager);
        var useCase = ctx.CreateUseCase();

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(
            ctx.Session.Id,
            new RequestApproveSessionJson { Mode = SessionApprovalMode.SingleActiveManager, Reason = "   " },
            ctx.Session.Version,
            "key-1"));

        exception.GetErrorMessages.ShouldBe([ResourcesErrorMessages.SESSION_SINGLE_MANAGER_APPROVAL_REQUIRES_REASON]);
        await ctx.BranchUsers.DidNotReceiveWithAnyArgs().CountActiveManagersByBranchIdAsNoTracking(Guid.Empty, CancellationToken.None);
    }

    [Fact]
    public async Task Execute_ShouldRefuse_WhenVersionIsStale()
    {
        var ctx = new Context(Role.Manager);
        var useCase = ctx.CreateUseCase();

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            useCase.Execute(ctx.Session.Id, new RequestApproveSessionJson(), ctx.Session.Version + 1, "key-1"));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_STALE_WRITE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRefuseMember_BeforeAnyReplay()
    {
        var ctx = new Context(Role.Member);
        var useCase = ctx.CreateUseCase();

        await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.Session.Id, new RequestApproveSessionJson(), ctx.Session.Version, "key-1"));

        await ctx.Idempotency.Requests.DidNotReceiveWithAnyArgs()
            .GetByScopeAsNoTracking(default!, default, default, default!, default);
    }

    [Fact]
    public async Task ExecuteBatch_ShouldApproveEverySession_WhenAllAreEligible()
    {
        var ctx = new Context(Role.Manager);
        var second = ctx.AddSiblingSession();
        var useCase = ctx.CreateUseCase();

        var response = await useCase.ExecuteBatch(ctx.Batch(ctx.Session, second), "key-1");

        response.Sessions.Count.ShouldBe(2);
        ctx.Session.Status.ShouldBe(DailyCloseStatus.Approved);
        second.Status.ShouldBe(DailyCloseStatus.Approved);
        await ctx.Events.Received(2).Add(Arg.Is<SessionEvent>(sessionEvent => sessionEvent.Kind == SessionEventKind.SessionApproved), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteBatch_ShouldApproveNothing_WhenOneSessionIsIneligible()
    {
        var ctx = new Context(Role.Manager);
        var second = ctx.AddSiblingSession();
        second.SubmittedByUserId = ctx.Approver.UserId;
        var useCase = ctx.CreateUseCase();

        var exception = await Should.ThrowAsync<BatchRejectedException>(() => useCase.ExecuteBatch(ctx.Batch(ctx.Session, second), "key-1"));

        exception.GetStatusCode.ShouldBe(403);
        exception.GetErrorMessages.ShouldBe([
            string.Format(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_ITEM_FAILED, second.Id, ResourcesErrorMessages.SESSION_APPROVER_IS_SUBMITTER)
        ]);
        ctx.Session.Status.ShouldBe(DailyCloseStatus.Submitted);
        second.Status.ShouldBe(DailyCloseStatus.Submitted);
        await ctx.UnitOfWork.DidNotReceive().Commit();
        await ctx.Events.DidNotReceiveWithAnyArgs().Add(default!, default);
        await ctx.Scope.DidNotReceive().Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteBatch_ShouldRefuse_SessionsOfDifferentCashboxDays()
    {
        var ctx = new Context(Role.Manager);
        var other = ctx.AddSiblingSession(otherCashbox: true);
        var useCase = ctx.CreateUseCase();

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.ExecuteBatch(ctx.Batch(ctx.Session, other), "key-1"));

        exception.GetErrorMessages.ShouldBe([ResourcesErrorMessages.SESSION_BATCH_APPROVAL_SCOPE_MIXED]);
    }

    [Fact]
    public async Task ExecuteBatch_ShouldRefuse_DuplicateSessions()
    {
        var ctx = new Context(Role.Manager);
        var useCase = ctx.CreateUseCase();

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.ExecuteBatch(ctx.Batch(ctx.Session, ctx.Session), "key-1"));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_DUPLICATE);
    }

    private static async Task ShouldRefuse(Context ctx, string message)
    {
        var useCase = ctx.CreateUseCase();
        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.Session.Id, new RequestApproveSessionJson(), ctx.Session.Version, "key-1"));
        exception.Message.ShouldBe(message);
        ctx.Session.Status.ShouldBe(DailyCloseStatus.Submitted);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private sealed class Context
    {
        public DateTime Now { get; } = new(2026, 4, 30, 14, 30, 0, DateTimeKind.Utc);
        public BranchUser Approver { get; }
        public Operator? ApproverOperator { get; }
        public Operator Custodian { get; }
        public Account Cashbox { get; }
        public DailyClose Session { get; set; }
        public List<DailyClose> Sessions { get; } = [];
        public IBranchUsersRepository BranchUsers { get; } = Substitute.For<IBranchUsersRepository>();
        public ISecurityCoordination Security { get; } = SecurityCoordinationBuilder.Build();
        public ISessionEventsRepository Events { get; } = Substitute.For<ISessionEventsRepository>();
        public IUnitOfWork UnitOfWork { get; } = new UnitOfWorkBuilder().Build();
        public FinancialCommandIdempotencyBuilder Idempotency { get; } = new();
        public IDailyCloseAccountCoordinationScope Scope { get; private set; } = null!;

        public Context(Role role, bool approverHasOperator = false)
        {
            Approver = new BranchUserBuilder().WithRole(role).Build();
            BranchUsers.GetActiveById(Approver.Id).Returns(Approver);
            ApproverOperator = approverHasOperator
                ? new OperatorBuilder().WithBranchId(Approver.BranchId).WithUserId(Approver.UserId).Build()
                : null;
            Cashbox = new AccountBuilder().WithBranchId(Approver.BranchId).Build();
            var memberUser = new UserBuilder().Build();
            Custodian = new OperatorBuilder().WithBranchId(Approver.BranchId).WithUser(memberUser).Build();
            Session = Rebuild();
        }

        public DailyClose Rebuild(Guid? custodianUserIdAtStart = null, Account? cashbox = null)
        {
            var built = new DailyCloseBuilder()
                .WithBranchId(Approver.BranchId)
                .WithAccount(cashbox ?? Cashbox)
                .WithDate(Now.Date)
                .WithStatus(DailyCloseStatus.Submitted)
                .WithCustodyState(SessionCustodyState.Finished)
                .WithCustodian(Custodian)
                .WithItemsFirstRecordedAt(Now.AddHours(-2))
                .WithRecordedBy(Custodian.User!, Custodian)
                .WithSubmittedBy(Custodian.User!, Custodian)
                .WithSubmittedAt(Now.AddHours(-1))
                .Build();
            var session = custodianUserIdAtStart is null
                ? built
                : new DailyClose
                {
                    Id = built.Id,
                    Version = built.Version,
                    BranchId = built.BranchId,
                    AccountId = built.AccountId,
                    Account = built.Account,
                    Date = built.Date,
                    Status = built.Status,
                    CustodyState = built.CustodyState,
                    CustodianOperatorId = built.CustodianOperatorId,
                    CustodianOperator = built.CustodianOperator,
                    CustodianUserIdAtStart = custodianUserIdAtStart,
                    RecordedByUserId = built.RecordedByUserId,
                    RecordedByOperatorId = built.RecordedByOperatorId,
                    SubmittedByUserId = built.SubmittedByUserId,
                    SubmittedByOperatorId = built.SubmittedByOperatorId,
                    ItemsFirstRecordedAt = built.ItemsFirstRecordedAt,
                    Sequence = built.Sequence,
                    StartedAt = built.StartedAt
                };
            Sessions.RemoveAll(existing => existing.Id == Session?.Id);
            Sessions.Add(session);
            return session;
        }

        public DailyClose AddSiblingSession(bool otherCashbox = false)
        {
            var cashbox = otherCashbox ? new AccountBuilder().WithBranchId(Approver.BranchId).Build() : Cashbox;
            var current = Session;
            var sibling = Rebuild(cashbox: cashbox);
            Sessions.Add(current);
            return sibling;
        }

        public RequestApproveSessionBatchJson Batch(params DailyClose[] sessions)
        {
            return new RequestApproveSessionBatchJson
            {
                Sessions = sessions
                    .Select(session => new RequestApproveSessionBatchItemJson { SessionId = session.Id, Version = session.Version })
                    .ToList()
            };
        }

        public ApproveSessionUseCase CreateUseCase()
        {
            var sessions = Substitute.For<IDailyClosesRepository>();
            sessions.GetByIdAndBranchId(Arg.Any<Guid>(), Approver.BranchId, Arg.Any<CancellationToken>())
                .Returns(call => Sessions.FirstOrDefault(session => session.Id == call.ArgAt<Guid>(0)));
            sessions.GetByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Approver.BranchId, Arg.Any<CancellationToken>())
                .Returns(call => Sessions.FirstOrDefault(session => session.Id == call.ArgAt<Guid>(0)));

            var operators = new OperatorsRepositoryBuilder()
                .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(Approver.UserId, Approver.BranchId, ApproverOperator)
                .Build();
            var operatorAccounts = ApproverOperator is null
                ? new OperatorAccountsRepositoryBuilder().Build()
                : new OperatorAccountsRepositoryBuilder().ListActiveByOperatorIdAsNoTracking(ApproverOperator.Id, []).Build();
            var settings = new SettingsRepositoryBuilder().GetByBranchIdAsNoTrackingReturns(Approver.BranchId, null).Build();
            var lockDateReader = new LockDateReader(settings);
            var coordination = new DailyCloseAccountCoordinationBuilder();
            var accountCoordination = coordination.Build();
            Scope = coordination.Scope;
            var clock = Substitute.For<IBranchClock>();
            clock.UtcNow().Returns(Now);
            var cashVarianceProductResolver = Substitute.For<ICashVarianceProductResolver>();
            cashVarianceProductResolver.GetIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

            return new ApproveSessionUseCase(
                new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(Approver).Build(),
                sessions,
                BranchUsers,
                Security,
                new MemberAccountScopeResolver(operators, operatorAccounts),
                new DailyCloseWorkflowGuard(clock),
                lockDateReader,
                new LockDateGuard(lockDateReader),
                cashVarianceProductResolver,
                new SessionEventRecorder(Events),
                Idempotency.Build(),
                accountCoordination,
                clock,
                UnitOfWork);
        }
    }
}
