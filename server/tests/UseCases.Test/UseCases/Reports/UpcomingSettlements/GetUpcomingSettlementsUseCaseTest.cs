using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.UpcomingSettlements;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.UpcomingSettlements;

public class GetUpcomingSettlementsUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AsOfDate = new(2026, 10, 20);

    [Fact]
    public async Task Execute_ShouldReadTheCallersBranch_WithWindowAndIndependentOverdueScopes()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var accountId = Guid.NewGuid();
        var request = new RequestUpcomingSettlementsJsonBuilder()
            .WithDueDateFrom(AsOfDate)
            .WithDueDateTo(AsOfDate.AddDays(13))
            .WithAccountId(accountId)
            .WithInstrument(SettlementInstrument.CreditCard)
            .WithPage(2)
            .WithPageSize(1)
            .Build();
        var window = new UpcomingSettlementFilter(AsOfDate, AsOfDate.AddDays(13), accountId, SettlementInstrument.CreditCard);
        var overdue = new UpcomingSettlementFilter(null, AsOfDate.AddDays(-1), accountId, SettlementInstrument.CreditCard);
        var row = new UpcomingSettlementRow(Guid.NewGuid(), Guid.NewGuid(), AsOfDate.AddDays(-2), AsOfDate.AddDays(1), 30m,
            SettlementInstrument.CreditCard, Guid.NewGuid(), "Cartão de Crédito", accountId, "T1", null, null);
        var transactions = new TransactionsRepositoryBuilder()
            .ListUpcomingSettlementTotalsByBranchIdAsNoTrackingReturns(branchUser.BranchId, window,
                [new UpcomingSettlementInstrumentTotalRow(SettlementInstrument.CreditCard, 3, 90m)])
            .ListUpcomingSettlementTotalsByBranchIdAsNoTrackingReturns(branchUser.BranchId, overdue,
                [new UpcomingSettlementInstrumentTotalRow(SettlementInstrument.CreditCard, 1, 7m)])
            .ListUpcomingSettlementsByBranchIdAsNoTrackingReturns(branchUser.BranchId, window, 2, 1, [row])
            .Build();
        var types = new TransactionTypesRepositoryBuilder()
            .ExistsWithSettlementInstrumentByBranchIdAsNoTracking(branchUser.BranchId, true)
            .Build();
        var accounts = new AccountsRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, new AccountBuilder().WithId(accountId).Build())
            .Build();

        var response = await CreateUseCase(branchUser, transactions, types, accounts).Execute(request);

        response.AsOfDate.ShouldBe(AsOfDate);
        response.IsConfigured.ShouldBeTrue();
        response.TotalCount.ShouldBe(3);
        response.TotalValue.ShouldBe(90m);
        response.OverdueCount.ShouldBe(1);
        response.OverdueValue.ShouldBe(7m);
        response.TotalPages.ShouldBe(3);
        response.HasNext.ShouldBeTrue();
        response.HasPrevious.ShouldBeTrue();
        response.Items.Single().TransactionId.ShouldBe(row.TransactionId);
        response.Items.Single().OriginTransactionId.ShouldBe(row.OriginTransactionId);
        // Every classified instrument in declared order, zeros included; None never appears.
        response.ByInstrument.Select(total => total.Instrument)
            .ShouldBe([SettlementInstrument.Cheque, SettlementInstrument.CreditCard, SettlementInstrument.DebitCard]);
        response.ByInstrument.Single(total => total.Instrument == SettlementInstrument.CreditCard).Value.ShouldBe(90m);
        response.ByInstrument.Single(total => total.Instrument == SettlementInstrument.CreditCard).Count.ShouldBe(3);
        response.ByInstrument.Where(total => total.Instrument != SettlementInstrument.CreditCard).ShouldAllBe(total => total.Count == 0 && total.Value == 0m);
        await transactions.Received(1).ListUpcomingSettlementTotalsByBranchIdAsNoTracking(
            branchUser.BranchId, overdue, Arg.Any<CancellationToken>());
        await transactions.DidNotReceive().ListUpcomingSettlementsByBranchIdAsNoTracking(
            Arg.Is<Guid>(id => id != branchUser.BranchId), Arg.Any<UpcomingSettlementFilter>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 3, false, true)]
    [InlineData(3, 3, true, false)]
    [InlineData(1, 4, false, false)]
    public async Task Execute_ShouldPageFromTheWholeFilterCount(int page, int count, bool hasPrevious, bool hasNext)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestUpcomingSettlementsJsonBuilder().WithPage(page).WithPageSize(count == 4 ? 4 : 1).Build();
        var window = new UpcomingSettlementFilter(request.DueDateFrom, request.DueDateTo, null, null);
        var transactions = new TransactionsRepositoryBuilder()
            .ListUpcomingSettlementTotalsByBranchIdAsNoTrackingReturns(branchUser.BranchId, window,
                [new UpcomingSettlementInstrumentTotalRow(SettlementInstrument.Cheque, count, 10m)])
            .ListUpcomingSettlementTotalsByBranchIdAsNoTrackingReturns(branchUser.BranchId,
                new UpcomingSettlementFilter(null, AsOfDate.AddDays(-1), null, null), [])
            .Build();

        var response = await CreateUseCase(branchUser, transactions,
            new TransactionTypesRepositoryBuilder().ExistsWithSettlementInstrumentByBranchIdAsNoTracking(branchUser.BranchId, true).Build(),
            new AccountsRepositoryBuilder().Build()).Execute(request);

        response.TotalPages.ShouldBe(count == 4 ? 1 : 3);
        (response.HasPrevious, response.HasNext).ShouldBe((hasPrevious, hasNext));
        response.OverdueCount.ShouldBe(0);
    }

    [Fact]
    public async Task Execute_ShouldReportNotConfigured_WhenNoBranchTypeCarriesAnInstrument()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var types = new TransactionTypesRepositoryBuilder()
            .ExistsWithSettlementInstrumentByBranchIdAsNoTracking(branchUser.BranchId, false)
            .Build();

        var response = await CreateUseCase(branchUser, new TransactionsRepositoryBuilder().Build(), types,
            new AccountsRepositoryBuilder().Build()).Execute(new RequestUpcomingSettlementsJsonBuilder().Build());

        response.IsConfigured.ShouldBeFalse();
        response.TotalCount.ShouldBe(0);
        response.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldNotStart_WhenTheRequestIsAlreadyCancelled()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var types = new TransactionTypesRepositoryBuilder().Build();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => CreateUseCase(branchUser, new TransactionsRepositoryBuilder().Build(),
            types, new AccountsRepositoryBuilder().Build()).Execute(new RequestUpcomingSettlementsJsonBuilder().Build(), cancelled.Token));

        await types.DidNotReceiveWithAnyArgs().ExistsWithSettlementInstrumentByBranchIdAsNoTracking(default);
    }

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var transactions = new TransactionsRepositoryBuilder().Build();

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => CreateUseCase(branchUser, transactions,
            new TransactionTypesRepositoryBuilder().Build(), new AccountsRepositoryBuilder().Build())
            .Execute(new RequestUpcomingSettlementsJsonBuilder().Build()));

        await transactions.DidNotReceiveWithAnyArgs().ListUpcomingSettlementTotalsByBranchIdAsNoTracking(default, default!, default);
    }

    [Fact]
    public async Task Execute_ShouldThrow404_WhenAccountIsNotTheBranchs()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var accountId = Guid.NewGuid();
        var accounts = new AccountsRepositoryBuilder().GetByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, null).Build();

        var exception = await Should.ThrowAsync<NotFoundException>(() => CreateUseCase(branchUser,
            new TransactionsRepositoryBuilder().Build(), new TransactionTypesRepositoryBuilder().Build(), accounts)
            .Execute(new RequestUpcomingSettlementsJsonBuilder().WithAccountId(accountId).Build()));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_BeforeAnyRead()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var types = new TransactionTypesRepositoryBuilder().Build();

        await Should.ThrowAsync<OnValidationException>(() => CreateUseCase(branchUser, new TransactionsRepositoryBuilder().Build(),
            types, new AccountsRepositoryBuilder().Build())
            .Execute(new RequestUpcomingSettlementsJsonBuilder().WithInstrument(SettlementInstrument.None).Build()));

        await types.DidNotReceiveWithAnyArgs().ExistsWithSettlementInstrumentByBranchIdAsNoTracking(default);
    }

    private static GetUpcomingSettlementsUseCase CreateUseCase(
        server.Domain.Entities.BranchUser branchUser,
        ITransactionsRepository transactions,
        ITransactionTypesRepository types,
        IAccountsRepository accounts)
    {
        var clock = Substitute.For<IBranchClock>();
        clock.UtcNow().Returns(FixedUtcNow);
        clock.LocalBusinessDate(FixedUtcNow).Returns(AsOfDate);
        return new GetUpcomingSettlementsUseCase(
            new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build(),
            accounts,
            new UpcomingSettlementsReader(transactions, types),
            clock);
    }
}
