using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseLedgerGuardTest
{
    [Theory]
    [InlineData(AccountType.Tab)]
    [InlineData(AccountType.BankAccount)]
    public async Task EnsureLedgerAcceptsNewRow_ShouldAllowNonTerminalAccountWithoutClose(AccountType accountType)
    {
        var dailyClosesRepository = Substitute.For<IDailyClosesRepository>();
        var guard = new DailyCloseLedgerGuard(
            dailyClosesRepository,
            Substitute.For<ITransactionsRepository>());

        await Should.NotThrowAsync(() => guard.EnsureLedgerAcceptsNewRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            accountType,
            DateTime.Today));

        await dailyClosesRepository.Received(1)
            .GetByBranchIdAndAccountIdAndDateAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureLedgerAcceptsNewRow_ShouldRequireOpenTerminalClose()
    {
        var guard = new DailyCloseLedgerGuard(
            Substitute.For<IDailyClosesRepository>(),
            Substitute.For<ITransactionsRepository>());

        var exception = await Should.ThrowAsync<ConflictException>(() => guard.EnsureLedgerAcceptsNewRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AccountType.Terminal,
            DateTime.Today));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_REQUIRES_OPEN_DAILY_CLOSE);
    }

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task EnsureLedgerAcceptsNewRow_ShouldFreezeNonDraftClose(DailyCloseStatus status)
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 30);
        var close = new DailyCloseBuilder()
            .WithBranchId(branchId)
            .WithAccountId(accountId)
            .WithDate(date)
            .WithStatus(status)
            .Build();
        var repository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(branchId, accountId, date, close)
            .Build();
        var guard = new DailyCloseLedgerGuard(repository, Substitute.For<ITransactionsRepository>());

        var exception = await Should.ThrowAsync<ConflictException>(() => guard.EnsureLedgerAcceptsNewRow(
            branchId,
            accountId,
            AccountType.Terminal,
            date));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
    }

    [Theory]
    [InlineData(AccountType.Tab)]
    [InlineData(AccountType.BankAccount)]
    public async Task EnsureLedgerAcceptsNewRow_ShouldFreezeExistingNonDraftNonTerminalClose(AccountType accountType)
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 30);
        var close = new DailyCloseBuilder()
            .WithBranchId(branchId)
            .WithAccountId(accountId)
            .WithDate(date)
            .WithStatus(DailyCloseStatus.Submitted)
            .Build();
        var repository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(branchId, accountId, date, close)
            .Build();
        var guard = new DailyCloseLedgerGuard(repository, Substitute.For<ITransactionsRepository>());

        var exception = await Should.ThrowAsync<ConflictException>(() => guard.EnsureLedgerAcceptsNewRow(
            branchId,
            accountId,
            accountType,
            date));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
    }

    [Fact]
    public async Task EnsureLedgerAcceptsNewRow_ShouldAllowDraftTerminalClose()
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 30);
        var close = new DailyCloseBuilder()
            .WithBranchId(branchId)
            .WithAccountId(accountId)
            .WithDate(date)
            .WithStatus(DailyCloseStatus.Draft)
            .Build();
        var repository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(branchId, accountId, date, close)
            .Build();
        var guard = new DailyCloseLedgerGuard(repository, Substitute.For<ITransactionsRepository>());

        await Should.NotThrowAsync(() => guard.EnsureLedgerAcceptsNewRow(
            branchId,
            accountId,
            AccountType.Terminal,
            date));
    }

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task EnsureLedgerIsMutable_ShouldRejectEveryNonDraftClose(DailyCloseStatus status)
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 30);
        var close = new DailyCloseBuilder()
            .WithBranchId(branchId)
            .WithAccountId(accountId)
            .WithDate(date)
            .WithStatus(status)
            .Build();
        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(branchId, accountId, date, close)
            .Build();
        var guard = new DailyCloseLedgerGuard(
            dailyClosesRepository,
            Substitute.For<ITransactionsRepository>());

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            guard.EnsureLedgerIsMutable(branchId, accountId, date));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureLedgerIsMutable_ShouldAllowDraftOrMissingClose(bool hasDraftClose)
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 30);
        var close = hasDraftClose
            ? new DailyCloseBuilder()
                .WithBranchId(branchId)
                .WithAccountId(accountId)
                .WithDate(date)
                .WithStatus(DailyCloseStatus.Draft)
                .Build()
            : null;
        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(branchId, accountId, date, close)
            .Build();
        var guard = new DailyCloseLedgerGuard(
            dailyClosesRepository,
            Substitute.For<ITransactionsRepository>());

        await Should.NotThrowAsync(() => guard.EnsureLedgerIsMutable(branchId, accountId, date));
    }

    [Fact]
    public async Task EnsureNoOutstandingDraftTransactions_ShouldRejectMatchingDraftRows()
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 30);
        var transactionsRepository = Substitute.For<ITransactionsRepository>();
        transactionsRepository
            .ExistsDraftByAccountAndDateAsNoTracking(branchId, accountId, date, Arg.Any<CancellationToken>())
            .Returns(true);
        var guard = new DailyCloseLedgerGuard(
            Substitute.For<IDailyClosesRepository>(),
            transactionsRepository);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            guard.EnsureNoOutstandingDraftTransactions(branchId, accountId, date));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS);
    }

    [Fact]
    public async Task EnsureNoOutstandingDraftTransactions_ShouldAllowWhenNoneExist()
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 30);
        var transactionsRepository = Substitute.For<ITransactionsRepository>();
        transactionsRepository
            .ExistsDraftByAccountAndDateAsNoTracking(branchId, accountId, date, Arg.Any<CancellationToken>())
            .Returns(false);
        var guard = new DailyCloseLedgerGuard(
            Substitute.For<IDailyClosesRepository>(),
            transactionsRepository);

        await Should.NotThrowAsync(() =>
            guard.EnsureNoOutstandingDraftTransactions(branchId, accountId, date));
    }
}
