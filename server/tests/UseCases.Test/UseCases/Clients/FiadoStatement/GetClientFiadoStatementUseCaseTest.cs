using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.Transactions;
using server.Application.UseCases.Clients.FiadoStatement;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Clients.FiadoStatement;

public class GetClientFiadoStatementUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2025, 6, 30, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2025, 6, 30);

    [Fact]
    public async Task Execute_ShouldComposePositionWindowAndPage_ForAManagerWithoutTouchingMemberScope()
    {
        var ctx = new TestContext(Role.Manager);
        var client = ctx.AddClient("Dona Rosa");
        var dateFrom = new DateTime(2025, 6, 1);
        ctx.Transactions.GetFiadoClientPositionAsNoTracking(ctx.BranchUser.BranchId, client.Id, Today, Arg.Any<CancellationToken>())
            .Returns(new FiadoClientPosition(40m, new DateTime(2025, 5, 2)));
        ctx.Transactions.GetFiadoStatementWindowByClientIdAsNoTracking(ctx.BranchUser.BranchId, client.Id, dateFrom, Today, Arg.Any<CancellationToken>())
            .Returns(new FiadoStatementWindow(100m, 40m, 3));
        ctx.Transactions.ListFiadoStatementByClientIdAsNoTracking(ctx.BranchUser.BranchId, client.Id, dateFrom, Today, 2, 2, Arg.Any<CancellationToken>())
            .Returns([new FiadoStatementRow(Guid.NewGuid(), dateFrom, Direction.In, 60m, Guid.NewGuid(), "Fiado B", "Recebimento", null, 40m)]);

        var response = await ctx.CreateUseCase().Execute(
            client.Id, new RequestClientFiadoStatementJson { DateFrom = dateFrom, Page = 2, PageSize = 2 });

        response.ClientName.ShouldBe("Dona Rosa");
        response.AsOfDate.ShouldBe(Today);
        response.DateFrom.ShouldBe(dateFrom);
        response.DateTo.ShouldBe(Today);
        response.OutstandingTotal.ShouldBe(40m);
        response.OldestUnpaidDate.ShouldBe(new DateTime(2025, 5, 2));
        response.OpeningBalance.ShouldBe(100m);
        response.ClosingBalance.ShouldBe(40m);
        response.Items.ShouldHaveSingleItem().RunningBalance.ShouldBe(40m);
        response.TotalCount.ShouldBe(3);
        response.TotalPages.ShouldBe(2);
        response.HasPrevious.ShouldBeTrue();
        response.HasNext.ShouldBeFalse();
        await ctx.ScopeResolver.DidNotReceiveWithAnyArgs().Resolve(default, default);
    }

    [Fact]
    public async Task Execute_ShouldAllowAMemberHoldingAnActiveTabGrant()
    {
        var ctx = new TestContext(Role.Member);
        var client = ctx.AddClient("Cliente");
        var tab = new AccountBuilder().WithBranchId(ctx.BranchUser.BranchId).WithType(AccountType.Tab).Build();
        ctx.GrantAccounts([tab]);
        ctx.Transactions.GetFiadoClientPositionAsNoTracking(default, default, default, default)
            .ReturnsForAnyArgs(new FiadoClientPosition(0m, null));
        ctx.Transactions.GetFiadoStatementWindowByClientIdAsNoTracking(default, default, default, default, default)
            .ReturnsForAnyArgs(new FiadoStatementWindow(0m, 0m, 0));
        ctx.Transactions.ListFiadoStatementByClientIdAsNoTracking(default, default, default, default, default, default, default)
            .ReturnsForAnyArgs([]);

        var response = await ctx.CreateUseCase().Execute(client.Id, new RequestClientFiadoStatementJson());

        response.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Execute_ShouldDenyAMemberWhoseOnlyGrantIsATerminal()
    {
        var ctx = new TestContext(Role.Member);
        var client = ctx.AddClient("Cliente");
        ctx.GrantAccounts([new AccountBuilder().WithBranchId(ctx.BranchUser.BranchId).WithType(AccountType.Terminal).Build()]);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => ctx.CreateUseCase().Execute(client.Id, new RequestClientFiadoStatementJson()));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_ACCESS_DENIED);
        await ctx.Transactions.DidNotReceiveWithAnyArgs()
            .ListFiadoStatementByClientIdAsNoTracking(default, default, default, default, default, default, default);
    }

    [Fact]
    public async Task Execute_ShouldDenyAMemberWithoutALinkedOperator_BeforeValidating()
    {
        var ctx = new TestContext(Role.Member);
        ctx.ScopeResolver.Resolve(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, Arg.Any<CancellationToken>())
            .Returns(new MemberAccountScope(null, []));

        await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => ctx.CreateUseCase().Execute(Guid.NewGuid(), new RequestClientFiadoStatementJson { PageSize = 500 }));
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_ForAClientOutsideTheBranch()
    {
        var ctx = new TestContext(Role.Admin);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => ctx.CreateUseCase().Execute(Guid.NewGuid(), new RequestClientFiadoStatementJson()));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_ForAnOversizedPage()
    {
        var ctx = new TestContext(Role.Manager);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => ctx.CreateUseCase().Execute(Guid.NewGuid(), new RequestClientFiadoStatementJson { PageSize = 101 }));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_PAGE_SIZE_INVALID);
    }

    [Fact]
    public async Task Execute_ShouldRejectADateFromAfterTheDefaultedDateTo_WithoutReading()
    {
        var ctx = new TestContext(Role.Manager);
        var client = ctx.AddClient("Cliente");

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => ctx.CreateUseCase().Execute(client.Id, new RequestClientFiadoStatementJson { DateFrom = Today.AddDays(1) }));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
        await ctx.Transactions.DidNotReceiveWithAnyArgs()
            .GetFiadoStatementWindowByClientIdAsNoTracking(default, default, default, default, default);
    }

    private sealed class TestContext
    {
        public TestContext(Role role)
        {
            BranchUser = new BranchUserBuilder().WithRole(role).Build();
            Authentication = Substitute.For<IAuthenticationService>();
            Authentication.GetAuthenticatedBranchUser().Returns(BranchUser);
            Clients = Substitute.For<IClientsRepository>();
            Accounts = Substitute.For<IAccountsRepository>();
            Transactions = Substitute.For<ITransactionsRepository>();
            ScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
            Clock = Substitute.For<IBranchClock>();
            Clock.UtcNow().Returns(FixedUtcNow);
            Clock.LocalBusinessDate(FixedUtcNow).Returns(Today);
        }

        public BranchUser BranchUser { get; }
        public IAuthenticationService Authentication { get; }
        public IClientsRepository Clients { get; }
        public IAccountsRepository Accounts { get; }
        public ITransactionsRepository Transactions { get; }
        public IMemberAccountScopeResolver ScopeResolver { get; }
        public IBranchClock Clock { get; }

        public Client AddClient(string name)
        {
            var client = new ClientBuilder().WithBranchId(BranchUser.BranchId).WithName(name).Build();
            Clients.GetByIdAndBranchIdAsNoTracking(client.Id, BranchUser.BranchId, Arg.Any<CancellationToken>()).Returns(client);
            return client;
        }

        public void GrantAccounts(IReadOnlyList<Account> accounts)
        {
            var linkedOperator = new OperatorBuilder().WithBranchId(BranchUser.BranchId).WithUserId(BranchUser.UserId).Build();
            ScopeResolver.Resolve(BranchUser.UserId, BranchUser.BranchId, Arg.Any<CancellationToken>())
                .Returns(new MemberAccountScope(linkedOperator, accounts.Select(account => account.Id).ToList()));
            Accounts.ListActiveByBranchId(BranchUser.BranchId).Returns(accounts);
        }

        public GetClientFiadoStatementUseCase CreateUseCase()
        {
            return new GetClientFiadoStatementUseCase(Authentication, Clients, Accounts, Transactions, ScopeResolver, Clock);
        }
    }
}
