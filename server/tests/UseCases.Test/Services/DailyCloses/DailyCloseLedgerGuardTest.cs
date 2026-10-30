using CommonTestUtilities.Entities;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseLedgerGuardTest
{
    private static readonly DateTime UtcNow = new(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 10, 29);

    [Theory]
    [InlineData(AccountType.Tab)]
    [InlineData(AccountType.BankAccount)]
    public async Task ResolveSessionForNewRow_ShouldBindNoSession_ForNonCashboxAccounts(AccountType accountType)
    {
        var ctx = new GuardContext(Role.Member);

        var resolution = await ctx.Guard.ResolveSessionForNewRow(Guid.NewGuid(), accountType, Today, ctx.RowContext());

        resolution.Session.ShouldBeNull();
        await ctx.DailyCloses.DidNotReceive()
            .GetActiveCustodyByAccountIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRequireAnOpeningAcknowledgment_WhenNoSessionIsActive()
    {
        var ctx = new GuardContext(Role.Member);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            ctx.Guard.ResolveSessionForNewRow(ctx.CashboxId, AccountType.Terminal, Today, ctx.RowContext()));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_REQUIRED);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldNotOpenCustodyForAnEarlierDate()
    {
        var ctx = new GuardContext(Role.Manager);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            ctx.Guard.ResolveSessionForNewRow(
                ctx.CashboxId,
                AccountType.Terminal,
                Today.AddDays(-1),
                ctx.RowContext(acknowledgment: new RequestSessionOpeningAcknowledgmentJson { ExplicitZero = true })));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_REQUIRES_SESSION);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldBindTheActiveSessionOfItsCustodian()
    {
        var ctx = new GuardContext(Role.Member);
        var active = ctx.ActiveSession(custodian: ctx.Operator);

        var resolution = await ctx.Guard.ResolveSessionForNewRow(ctx.CashboxId, AccountType.Terminal, Today, ctx.RowContext());

        resolution.SessionId.ShouldBe(active.Id);
        resolution.Started.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRefuseACompetingAcknowledgment_WhenASessionIsAlreadyActive()
    {
        var ctx = new GuardContext(Role.Member);
        ctx.ActiveSession(custodian: ctx.Operator);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            ctx.Guard.ResolveSessionForNewRow(
                ctx.CashboxId,
                AccountType.Terminal,
                Today,
                ctx.RowContext(acknowledgment: new RequestSessionOpeningAcknowledgmentJson { ExplicitZero = true })));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_NOT_ALLOWED);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRefuseTodayRows_WhileYesterdaysCustodyIsStillActive()
    {
        var ctx = new GuardContext(Role.Member);
        ctx.ActiveSession(custodian: ctx.Operator, date: Today.AddDays(-1));

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            ctx.Guard.ResolveSessionForNewRow(ctx.CashboxId, AccountType.Terminal, Today, ctx.RowContext()));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_CUSTODY_PREVIOUS_DAY);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRefuseAMemberWhoIsNotTheCustodian()
    {
        var ctx = new GuardContext(Role.Member);
        ctx.ActiveSession(custodian: new OperatorBuilder().Build());

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            ctx.Guard.ResolveSessionForNewRow(ctx.CashboxId, AccountType.Terminal, Today, ctx.RowContext()));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_NOT_CUSTODIAN);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldLetAManagerRecordForTheActiveCustodian()
    {
        var ctx = new GuardContext(Role.Manager);
        var active = ctx.ActiveSession(custodian: new OperatorBuilder().Build());

        var resolution = await ctx.Guard.ResolveSessionForNewRow(ctx.CashboxId, AccountType.Terminal, Today, ctx.RowContext());

        resolution.SessionId.ShouldBe(active.Id);
    }

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    public async Task ResolveSessionForNewRow_ShouldRefuseAFrozenActiveSession(DailyCloseStatus status)
    {
        var ctx = new GuardContext(Role.Member);
        ctx.ActiveSession(custodian: ctx.Operator, status: status);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            ctx.Guard.ResolveSessionForNewRow(ctx.CashboxId, AccountType.Terminal, Today, ctx.RowContext()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRefuseAMemberNamingAHistoricalSession()
    {
        var ctx = new GuardContext(Role.Member);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            ctx.Guard.ResolveSessionForNewRow(
                ctx.CashboxId,
                AccountType.Terminal,
                Today,
                ctx.RowContext(sessionId: Guid.NewGuid(), reason: "esquecido")));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_NOT_ALLOWED);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRequireAReason_ForAHistoricalWrite()
    {
        var ctx = new GuardContext(Role.Manager);

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            ctx.Guard.ResolveSessionForNewRow(
                ctx.CashboxId,
                AccountType.Terminal,
                Today,
                ctx.RowContext(sessionId: Guid.NewGuid())));

        exception.GetErrorMessages.ShouldHaveSingleItem().ShouldBe(ResourcesErrorMessages.SESSION_HISTORICAL_WRITE_REQUIRES_REASON);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldBindTheNamedDraftSession_OnItsOwnDate()
    {
        var ctx = new GuardContext(Role.Manager);
        var named = ctx.Session(Today.AddDays(-2), DailyCloseStatus.Draft, SessionCustodyState.Finished);

        var resolution = await ctx.Guard.ResolveSessionForNewRow(
            ctx.CashboxId,
            AccountType.Terminal,
            Today.AddDays(-2),
            ctx.RowContext(sessionId: named.Id, reason: "venda esquecida"));

        resolution.SessionId.ShouldBe(named.Id);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRefuseAHistoricalRowDatedOutsideItsSession()
    {
        var ctx = new GuardContext(Role.Manager);
        var named = ctx.Session(Today.AddDays(-2), DailyCloseStatus.Draft, SessionCustodyState.Finished);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            ctx.Guard.ResolveSessionForNewRow(
                ctx.CashboxId,
                AccountType.Terminal,
                Today.AddDays(-1),
                ctx.RowContext(sessionId: named.Id, reason: "venda esquecida")));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_HISTORICAL_DATE_MISMATCH);
    }

    [Fact]
    public async Task ResolveSessionForNewRow_ShouldRefuseAnotherCashboxesSession()
    {
        var ctx = new GuardContext(Role.Manager);
        var named = ctx.Session(Today, DailyCloseStatus.Draft, SessionCustodyState.Finished);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            ctx.Guard.ResolveSessionForNewRow(
                Guid.NewGuid(),
                AccountType.Terminal,
                Today,
                ctx.RowContext(sessionId: named.Id, reason: "venda esquecida")));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_NOT_FOR_ACCOUNT);
    }

    [Fact]
    public async Task EnsureLedgerIsMutable_ShouldFollowTheRowsOwnSession()
    {
        var ctx = new GuardContext(Role.Manager);
        var submitted = ctx.Session(Today, DailyCloseStatus.Submitted, SessionCustodyState.Active);
        var draft = ctx.Session(Today, DailyCloseStatus.Draft, SessionCustodyState.Finished);

        await Should.NotThrowAsync(() => ctx.Guard.EnsureLedgerIsMutable(Row(ctx, draft.Id)));
        var exception = await Should.ThrowAsync<ConflictException>(() => ctx.Guard.EnsureLedgerIsMutable(Row(ctx, submitted.Id)));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
    }

    [Fact]
    public async Task EnsureLedgerIsMutable_ShouldRefuseAnUnresolvedLegacyCashboxRow()
    {
        var ctx = new GuardContext(Role.Manager);
        ctx.Accounts.GetByIdAndBranchIdAsNoTracking(ctx.CashboxId, ctx.BranchId, Arg.Any<CancellationToken>())
            .Returns(new Account { Id = ctx.CashboxId, BranchId = ctx.BranchId, Type = AccountType.Terminal });

        var exception = await Should.ThrowAsync<ConflictException>(() => ctx.Guard.EnsureLedgerIsMutable(Row(ctx, null)));

        exception.Message.ShouldBe(ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE);
    }

    [Fact]
    public async Task EnsureNoOutstandingDraftTransactions_ShouldThrow_WhenTheSessionHasDraftRows()
    {
        var ctx = new GuardContext(Role.Manager);
        var session = ctx.Session(Today, DailyCloseStatus.Draft, SessionCustodyState.Active);
        ctx.Transactions.ExistsDraftBySessionAsNoTracking(session, Arg.Any<CancellationToken>()).Returns(true);

        var exception = await Should.ThrowAsync<ConflictException>(() => ctx.Guard.EnsureNoOutstandingDraftTransactions(session));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS);
    }

    private static Transaction Row(GuardContext ctx, Guid? sessionId) => new()
    {
        BranchId = ctx.BranchId,
        AccountId = ctx.CashboxId,
        Date = Today,
        SessionId = sessionId
    };

    private sealed class GuardContext
    {
        public GuardContext(Role role)
        {
            Operator = new OperatorBuilder().WithBranchId(BranchId).Build();
            Actor = new BranchUserBuilder().WithBranchId(BranchId).WithRole(role).Build();
            var clock = Substitute.For<IBranchClock>();
            clock.UtcNow().Returns(UtcNow);
            clock.LocalBusinessDate(Arg.Any<DateTime>()).Returns(call => call.Arg<DateTime>().AddHours(-3).Date);
            Guard = new DailyCloseLedgerGuard(
                DailyCloses,
                Transactions,
                Accounts,
                Substitute.For<ISessionHandoversRepository>(),
                null!,
                null!,
                clock);
        }

        public Guid BranchId { get; } = Guid.NewGuid();
        public Guid CashboxId { get; } = Guid.NewGuid();
        public Operator Operator { get; }
        public BranchUser Actor { get; }
        public IDailyClosesRepository DailyCloses { get; } = Substitute.For<IDailyClosesRepository>();
        public ITransactionsRepository Transactions { get; } = Substitute.For<ITransactionsRepository>();
        public IAccountsRepository Accounts { get; } = Substitute.For<IAccountsRepository>();
        public DailyCloseLedgerGuard Guard { get; }

        public SessionRowContext RowContext(
            Guid? sessionId = null,
            string? reason = null,
            RequestSessionOpeningAcknowledgmentJson? acknowledgment = null)
        {
            return new SessionRowContext(Actor, new MemberAccountScope(Operator, [CashboxId]), sessionId, reason, acknowledgment);
        }

        public DailyClose ActiveSession(
            Operator custodian,
            DateTime? date = null,
            DailyCloseStatus status = DailyCloseStatus.Draft)
        {
            var session = new DailyCloseBuilder()
                .WithBranchId(BranchId)
                .WithAccountId(CashboxId)
                .WithDate(date ?? Today)
                .WithStatus(status)
                .WithCustodian(custodian)
                .Build();
            DailyCloses.GetActiveCustodyByAccountIdAsNoTracking(BranchId, CashboxId, Arg.Any<CancellationToken>()).Returns(session);
            return session;
        }

        public DailyClose Session(DateTime date, DailyCloseStatus status, SessionCustodyState custodyState)
        {
            var session = new DailyCloseBuilder()
                .WithBranchId(BranchId)
                .WithAccountId(CashboxId)
                .WithDate(date)
                .WithStatus(status)
                .WithCustodyState(custodyState)
                .WithCustodian(Operator)
                .Build();
            DailyCloses.GetByIdAndBranchIdAsNoTracking(session.Id, BranchId, Arg.Any<CancellationToken>()).Returns(session);
            return session;
        }
    }
}
