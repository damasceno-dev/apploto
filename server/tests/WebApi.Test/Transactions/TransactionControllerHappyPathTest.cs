using System.Globalization;
using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldReturn201AndPersistDenormalizedFields_WhenManagerCreatesTransaction()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateMgr", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(DateTime.Today)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.BranchId.ShouldBe(branch.Id);
        payload.CategoryId.ShouldBe(category.Id);
        payload.Direction.ShouldBe(Direction.In);
        payload.Status.ShouldBe(TransactionStatus.Active);
        payload.DueDate.ShouldBe(request.Date);
        payload.RecordedByOperatorId.ShouldBe(op.Id);
        payload.CreatedByUserId.ShouldBe(user.Id);

        var persisted = await factory.ReloadAsync<Transaction>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.CategoryId.ShouldBe(category.Id);
        persisted.Direction.ShouldBe(Direction.In);
        persisted.Value.ShouldBe(request.Value);
        persisted.Status.ShouldBe(TransactionStatus.Active);
    }

    [Fact]
    public async Task Create_ShouldReturn201AndSaveAsDraft_WhenSaveAsDraftIsTrue()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateDraft", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(DateTime.Today)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithSaveAsDraft(true)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        payload.Status.ShouldBe(TransactionStatus.Draft);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenMemberActsOnLinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateMemberLinked", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas");
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.SameDay);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(DateTime.Today)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        payload.RecordedByOperatorId.ShouldBe(op.Id);

        var persisted = await factory.ReloadAsync<Transaction>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.RecordedByOperatorId.ShouldBe(op.Id);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn201AndPersistManualInstallmentRows_WhenChequeType()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnInstallment", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);

        var firstDueDate = DateTime.Today.AddDays(30);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithDescription("Cheque da promoção")
            .WithValue(100.01m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithInstallments(
            [
                new()
                {
                    DueDate = firstDueDate,
                    Value = 33.33m
                },
                new()
                {
                    DueDate = firstDueDate.AddDays(12),
                    Value = 33.33m
                },
                new()
                {
                    DueDate = firstDueDate.AddDays(40),
                    Value = 33.35m
                }
            ])
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        payload.Installments.Count.ShouldBe(3);
        payload.Installments.Sum(installment => installment.Value).ShouldBe(request.Value);

        var originId = payload.Installments[0].Id;
        var persisted = await ListInstallmentsAsync(originId, branch.Id);

        persisted.Count.ShouldBe(3);
        persisted.All(row => row.OriginTransactionId == originId).ShouldBeTrue();
        persisted.Sum(row => row.Value).ShouldBe(request.Value);
        persisted.Select(row => row.DueDate).ShouldBe(request.Installments.Select(item => item.DueDate));
        persisted.All(row => row.RecordedByOperatorId == op.Id).ShouldBeTrue();
        persisted[0].Description.ShouldBe("CH PRE (1/3) - Cheque da promoção");
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn201AndSaveAllManualRowsAsDraft_WhenSaveAsDraftIsTrue()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnInstallmentManualDraft", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);

        var firstDueDate = DateTime.Today.AddDays(30);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(300m)
            .WithSaveAsDraft(true)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate, Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(30), Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(60), Value = 100m }
            ])
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        payload.Installments.Count.ShouldBe(3);

        var originId = payload.Installments[0].Id;
        var persisted = await ListInstallmentsAsync(originId, branch.Id);

        persisted.Count.ShouldBe(3);
        persisted.ShouldAllBe(row => row.Status == TransactionStatus.Draft);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn201AndSaveAllAutoGeneratedRowsAsDraft_WhenSaveAsDraftIsTrue()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnInstallmentAutoDraft", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(400m)
            .WithSaveAsDraft(true)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithAutoGeneration(4, DateTime.Today.AddDays(30))
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        payload.Installments.Count.ShouldBe(4);

        var originId = payload.Installments[0].Id;
        var persisted = await ListInstallmentsAsync(originId, branch.Id);

        persisted.Count.ShouldBe(4);
        persisted.ShouldAllBe(row => row.Status == TransactionStatus.Draft);
    }

    [Fact]
    public async Task Create_ShouldReturn201WithDueDateSkippingBranchHoliday_WhenNextBusinessDayRule()
    {
        // Friday transaction date with NextBusinessDay would normally land on the
        // following Monday. Seeding that Monday as a branch holiday must shift the
        // persisted due date to Tuesday.
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnCreateHolidayNext", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, settlementRule: SettlementRule.NextBusinessDay);

        var friday = NextDayOfWeekFuture(DayOfWeek.Friday);
        var holidayMonday = friday.AddDays(3);
        await factory.SeedHolidayAsync(branch.Id, holidayMonday);

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(friday)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        payload.DueDate.ShouldBe(friday.AddDays(4)); // Tuesday

        var persisted = await factory.ReloadAsync<Transaction>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.DueDate.ShouldBe(payload.DueDate);
    }

    [Fact]
    public async Task CreateInstallment_ShouldReturn201WithAutoGeneratedDueDateSkippingBranchHoliday()
    {
        // Auto-staggered cheque installments use the first due date plus one month per
        // subsequent row. If a row lands on a branch holiday, AdjustToNextBusinessDay
        // moves it forward to the next non-weekend, non-holiday business day.
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnInstallmentHoliday", Role.Manager);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(
            category.Id,
            settlementRule: SettlementRule.OperatorEnteredCheque);

        var firstDueDate = NextDayOfWeekFuture(DayOfWeek.Tuesday);
        var secondInstallmentSeed = firstDueDate.AddMonths(1);
        await factory.SeedHolidayAsync(branch.Id, secondInstallmentSeed);

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(200m)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(account.Id)
            .WithAutoGeneration(2, firstDueDate)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/transaction/installment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateTransactionInstallmentJson>();
        payload.Installments.Count.ShouldBe(2);

        var originId = payload.Installments[0].Id;
        var persisted = await ListInstallmentsAsync(originId, branch.Id);
        persisted.Count.ShouldBe(2);
        persisted[0].DueDate.ShouldBe(firstDueDate);
        persisted[1].DueDate.ShouldBeGreaterThan(secondInstallmentSeed);
        persisted[1].DueDate.DayOfWeek.ShouldNotBe(DayOfWeek.Saturday);
        persisted[1].DueDate.DayOfWeek.ShouldNotBe(DayOfWeek.Sunday);
        persisted[1].DueDate.ShouldNotBe(secondInstallmentSeed);
    }

    private static DateTime NextDayOfWeekFuture(DayOfWeek dayOfWeek)
    {
        var result = DateTime.Today.AddDays(7);
        while (result.DayOfWeek != dayOfWeek)
        {
            result = result.AddDays(1);
        }
        return result;
    }

    [Fact]
    public async Task Get_ShouldReturn200WithTransaction_WhenManagerGetsExistingTransaction()
    {
        var ctx = await SeedReadContextAsync("TxnGetHappy");
        var transaction = await SeedReadTransactionAsync(
            ctx,
            date: new DateTime(2025, 5, 10),
            value: 123.45m,
            description: "single-view");

        var httpResponse = await _client.GetAuthAsync($"/transaction/{transaction.Id}", ctx.Token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionJson>();
        payload.Id.ShouldBe(transaction.Id);
        payload.Date.ShouldBe(transaction.Date);
        payload.Value.ShouldBe(transaction.Value);
        payload.Description.ShouldBe(transaction.Description);
        payload.AccountId.ShouldBe(ctx.Account.Id);
        payload.TransactionTypeId.ShouldBe(ctx.TransactionType.Id);
        payload.BranchId.ShouldBe(ctx.Branch.Id);
    }

    [Fact]
    public async Task List_ShouldReturnOnlyAuthenticatedBranchRows()
    {
        var ctxA = await SeedReadContextAsync("TxnListBranchA");
        var ctxB = await SeedReadContextAsync("TxnListBranchB");
        var rowA = await SeedReadTransactionAsync(ctxA);
        var rowB = await SeedReadTransactionAsync(ctxB);

        var payload = await GetListAsync("/transaction", ctxA.Token);

        payload.Items.Select(item => item.Id).ShouldContain(rowA.Id);
        payload.Items.Select(item => item.Id).ShouldNotContain(rowB.Id);
        payload.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_ShouldApplySupportedFilters()
    {
        var ctx = await SeedReadContextAsync("TxnListFilters");
        var otherAccount = await factory.SeedAccountAsync(ctx.Branch.Id, AccountType.Terminal);
        var otherOperator = await factory.SeedOperatorAsync(ctx.Branch.Id);
        var client = await factory.SeedClientAsync(ctx.Branch.Id, "Filter Client");
        var otherClient = await factory.SeedClientAsync(ctx.Branch.Id, "Other Filter Client");

        var accountMatch = await SeedReadTransactionAsync(
            ctx,
            date: new DateTime(2025, 2, 10),
            clientId: client.Id,
            description: "account-match");
        var accountMiss = await SeedReadTransactionAsync(
            ctx,
            accountId: otherAccount.Id,
            date: new DateTime(2025, 2, 10),
            clientId: client.Id,
            description: "account-miss");
        var cancelled = await SeedReadTransactionAsync(
            ctx,
            date: new DateTime(2025, 2, 11),
            clientId: client.Id,
            status: TransactionStatus.Cancelled,
            description: "cancelled");
        var outsideDate = await SeedReadTransactionAsync(
            ctx,
            date: new DateTime(2025, 1, 1),
            clientId: client.Id,
            description: "outside-date");
        var operatorMatch = await SeedReadTransactionAsync(
            ctx,
            recordedByOperatorId: otherOperator.Id,
            date: new DateTime(2025, 2, 12),
            clientId: client.Id,
            description: "operator-match");
        var clientMatch = await SeedReadTransactionAsync(
            ctx,
            date: new DateTime(2025, 2, 13),
            clientId: otherClient.Id,
            description: "client-match");

        var byAccount = await GetListAsync($"/transaction?accountId={ctx.Account.Id}", ctx.Token);
        byAccount.Items.Select(item => item.Id).ShouldContain(accountMatch.Id);
        byAccount.Items.Select(item => item.Id).ShouldNotContain(accountMiss.Id);
        byAccount.Items.All(item => item.AccountId == ctx.Account.Id).ShouldBeTrue();

        var byStatus = await GetListAsync($"/transaction?status={(int)TransactionStatus.Cancelled}", ctx.Token);
        byStatus.Items.Select(item => item.Id).ShouldBe([cancelled.Id]);

        var byDateRange = await GetListAsync(
            $"/transaction?dateFrom={QueryDate(new DateTime(2025, 2, 10))}&dateTo={QueryDate(new DateTime(2025, 2, 12))}",
            ctx.Token);
        byDateRange.Items.Select(item => item.Id).ShouldContain(accountMatch.Id);
        byDateRange.Items.Select(item => item.Id).ShouldContain(cancelled.Id);
        byDateRange.Items.Select(item => item.Id).ShouldContain(operatorMatch.Id);
        byDateRange.Items.Select(item => item.Id).ShouldNotContain(outsideDate.Id);
        byDateRange.Items.Select(item => item.Id).ShouldNotContain(clientMatch.Id);

        var byOperator = await GetListAsync($"/transaction?operatorId={otherOperator.Id}", ctx.Token);
        byOperator.Items.Select(item => item.Id).ShouldBe([operatorMatch.Id]);

        var byClient = await GetListAsync($"/transaction?clientId={otherClient.Id}", ctx.Token);
        byClient.Items.Select(item => item.Id).ShouldBe([clientMatch.Id]);
    }

    [Fact]
    public async Task List_ShouldResolveMineToAuthenticatedLinkedOperator()
    {
        var ctx = await SeedReadContextAsync("TxnListMine");
        var otherOperator = await factory.SeedOperatorAsync(ctx.Branch.Id);

        var ownRow = await SeedReadTransactionAsync(
            ctx,
            recordedByOperatorId: ctx.Operator.Id,
            description: "mine");
        var otherRow = await SeedReadTransactionAsync(
            ctx,
            recordedByOperatorId: otherOperator.Id,
            description: "not-mine");

        var payload = await GetListAsync("/transaction?mine=true", ctx.Token);

        payload.Items.Select(item => item.Id).ShouldBe([ownRow.Id]);
        payload.Items.Select(item => item.Id).ShouldNotContain(otherRow.Id);
        payload.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_ShouldNarrowMemberRowsToLinkedAccounts()
    {
        var ctx = await SeedReadContextAsync("TxnListMember", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(ctx.Branch.Id, userId: ctx.User.Id);
        var otherOperator = await factory.SeedOperatorAsync(ctx.Branch.Id);
        var accountA = await factory.SeedAccountAsync(ctx.Branch.Id, AccountType.Terminal);
        var accountB = await factory.SeedAccountAsync(ctx.Branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, accountA.Id);
        await factory.SeedOperatorAccountAsync(otherOperator.Id, accountB.Id);

        var visible = await SeedReadTransactionAsync(ctx, accountId: accountA.Id, recordedByOperatorId: callerOperator.Id);
        var hidden = await SeedReadTransactionAsync(ctx, accountId: accountB.Id, recordedByOperatorId: otherOperator.Id);

        var payload = await GetListAsync("/transaction", ctx.Token);

        payload.Items.Select(item => item.Id).ShouldBe([visible.Id]);
        payload.Items.Select(item => item.Id).ShouldNotContain(hidden.Id);
        payload.TotalCount.ShouldBe(1);

        var explicitUnlinked = await GetListAsync($"/transaction?accountId={accountB.Id}", ctx.Token);
        explicitUnlinked.Items.ShouldBeEmpty();
        explicitUnlinked.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task List_ShouldOrderByDateTimeCreatedAtAndIdDescending()
    {
        var ctx = await SeedReadContextAsync("TxnListOrdering");
        var sameCreatedLowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sameCreatedHighId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var dateWins = await SeedReadTransactionAsync(
            ctx,
            id: Guid.Parse("00000000-0000-0000-0000-000000000010"),
            date: new DateTime(2025, 3, 2),
            transactionTime: new TimeOnly(8, 0),
            createdAt: Utc(2025, 3, 1, 8));
        var timeWins = await SeedReadTransactionAsync(
            ctx,
            id: Guid.Parse("00000000-0000-0000-0000-000000000011"),
            date: new DateTime(2025, 3, 1),
            transactionTime: new TimeOnly(10, 0),
            createdAt: Utc(2025, 3, 1, 8));
        var createdAtWins = await SeedReadTransactionAsync(
            ctx,
            id: Guid.Parse("00000000-0000-0000-0000-000000000012"),
            date: new DateTime(2025, 3, 1),
            transactionTime: new TimeOnly(9, 0),
            createdAt: Utc(2025, 3, 4, 8));
        var idWinsHigh = await SeedReadTransactionAsync(
            ctx,
            id: sameCreatedHighId,
            date: new DateTime(2025, 3, 1),
            transactionTime: new TimeOnly(9, 0),
            createdAt: Utc(2025, 3, 3, 8));
        var idWinsLow = await SeedReadTransactionAsync(
            ctx,
            id: sameCreatedLowId,
            date: new DateTime(2025, 3, 1),
            transactionTime: new TimeOnly(9, 0),
            createdAt: Utc(2025, 3, 3, 8));
        var nullTimeLast = await SeedReadTransactionAsync(
            ctx,
            id: Guid.Parse("00000000-0000-0000-0000-000000000013"),
            date: new DateTime(2025, 3, 1),
            transactionTime: null,
            createdAt: Utc(2025, 3, 5, 8));

        var payload = await GetListAsync("/transaction", ctx.Token);

        payload.Items.Select(item => item.Id).ShouldBe(
        [
            dateWins.Id,
            timeWins.Id,
            createdAtWins.Id,
            idWinsHigh.Id,
            idWinsLow.Id,
            nullTimeLast.Id
        ]);
    }

    [Fact]
    public async Task List_ShouldPageWithTotalCountFromFullFilteredSetAndExposePagingMetadata()
    {
        var ctx = await SeedReadContextAsync("TxnListPagination");
        foreach (var index in Enumerable.Range(1, 7))
        {
            await SeedReadTransactionAsync(
                ctx,
                date: new DateTime(2025, 4, index),
                value: index,
                description: $"page-{index}");
        }

        var page1 = await GetListAsync($"/transaction?accountId={ctx.Account.Id}&page=1&pageSize=3", ctx.Token);
        var page2 = await GetListAsync($"/transaction?accountId={ctx.Account.Id}&page=2&pageSize=3", ctx.Token);
        var page3 = await GetListAsync($"/transaction?accountId={ctx.Account.Id}&page=3&pageSize=3", ctx.Token);

        page1.Items.Count.ShouldBe(3);
        page1.TotalCount.ShouldBe(7);
        page1.TotalPages.ShouldBe(3);
        page1.HasNext.ShouldBeTrue();
        page1.HasPrevious.ShouldBeFalse();

        page2.Items.Count.ShouldBe(3);
        page2.TotalCount.ShouldBe(7);
        page2.TotalPages.ShouldBe(3);
        page2.HasNext.ShouldBeTrue();
        page2.HasPrevious.ShouldBeTrue();

        page3.Items.Count.ShouldBe(1);
        page3.TotalCount.ShouldBe(7);
        page3.TotalPages.ShouldBe(3);
        page3.HasNext.ShouldBeFalse();
        page3.HasPrevious.ShouldBeTrue();
    }

    [Fact]
    public async Task List_ShouldExposeJoinedNamesOnItems()
    {
        var ctx = await SeedReadContextAsync("TxnListNames");
        var client = await factory.SeedClientAsync(ctx.Branch.Id, "Maria Cliente");
        await SeedReadTransactionAsync(
            ctx,
            date: new DateTime(2025, 5, 7),
            clientId: client.Id,
            description: "named");

        var payload = await GetListAsync($"/transaction?accountId={ctx.Account.Id}", ctx.Token);

        payload.Items.Count.ShouldBe(1);
        var item = payload.Items[0];
        item.AccountName.ShouldBe(ctx.Account.Name);
        item.ClientName.ShouldBe(client.Name);
        item.TransactionTypeName.ShouldBe(ctx.TransactionType.Name);
    }

    [Fact]
    public async Task List_ShouldShowSharedTerminalAccountRowsAcrossMembers_RegardlessOfRecordedByOperator()
    {
        // Shared terminal: a single account is linked to BOTH Member A and Member B.
        // 5.5.4 policy: a Member sees every row on a linked account, even rows recorded
        // by the OTHER operator on the SAME account.
        var (userA, branch, _, tokenA) = await factory.SeedFullBranchContextAsync("TxnListSharedTerminal", Role.Member);
        var operatorA = await factory.SeedOperatorAsync(branch.Id, userId: userA.Id);
        var userB = await factory.SeedUserAsync();
        var membershipB = await factory.SeedBranchUserAsync(userB.Id, branch.Id, Role.Member);
        var operatorB = await factory.SeedOperatorAsync(branch.Id, userId: userB.Id);
        var sharedAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(operatorA.Id, sharedAccount.Id);
        await factory.SeedOperatorAccountAsync(operatorB.Id, sharedAccount.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Shared Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        var rowFromA = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: sharedAccount.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorA.Id,
            createdByUserId: userA.Id);
        var rowFromB = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: sharedAccount.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorB.Id,
            createdByUserId: userB.Id);

        var payload = await GetListAsync("/transaction", tokenA);

        payload.Items.Select(item => item.Id).ShouldContain(rowFromA.Id);
        payload.Items.Select(item => item.Id).ShouldContain(rowFromB.Id);
        payload.TotalCount.ShouldBe(2);
        // Sanity: the second member token also sees both rows.
        var tokenB = factory.IssueBranchToken(membershipB);
        var payloadB = await GetListAsync("/transaction", tokenB);
        payloadB.Items.Select(item => item.Id).ShouldContain(rowFromA.Id);
        payloadB.Items.Select(item => item.Id).ShouldContain(rowFromB.Id);
    }

    [Fact]
    public async Task List_ShouldReturn200WithEmptyItems_WhenMemberHasLinkedOperatorButNoActiveAccounts()
    {
        // 5.5.21 parity for the use-case empty-scope short-circuit (5.5.3): the Member
        // has a linked operator but zero active OperatorAccount rows. The HTTP pipeline
        // must mirror the use case and return 200 OK with Items=[] and TotalCount=0.
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnListEmptyScope", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        // No SeedOperatorAccountAsync calls — operator has zero active accounts.
        var category = await factory.SeedCategoryAsync(branch.Id, "EmptyScope Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        // Seed an unrelated transaction in the same branch by another operator on
        // an account NOT linked to the Member, to prove the short-circuit isn't
        // accidentally returning rows.
        var otherOperator = await factory.SeedOperatorAsync(branch.Id);
        var otherAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: otherAccount.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: otherOperator.Id,
            createdByUserId: user.Id);

        var payload = await GetListAsync("/transaction", token);

        payload.Items.ShouldBeEmpty();
        payload.TotalCount.ShouldBe(0);
        payload.TotalPages.ShouldBe(0);
        payload.HasNext.ShouldBeFalse();
        payload.HasPrevious.ShouldBeFalse();
    }

    private async Task<ResponseListTransactionsJson> GetListAsync(string uri, string token)
    {
        var response = await _client.GetAuthAsync(uri, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseListTransactionsJson>();
    }

    private async Task<LedgerContext> SeedReadContextAsync(string label, Role role = Role.Manager)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, role);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: role == Role.Member ? null : user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, $"{label} Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        return new LedgerContext(user, branch, token, op, account, category, transactionType);
    }

    private Task<Transaction> SeedReadTransactionAsync(
        LedgerContext ctx,
        Guid? id = null,
        Guid? accountId = null,
        Guid? recordedByOperatorId = null,
        DateTime? date = null,
        decimal value = 10m,
        string? description = null,
        TimeOnly? transactionTime = null,
        Guid? clientId = null,
        TransactionStatus status = TransactionStatus.Active,
        DateTime? createdAt = null)
    {
        return factory.SeedTransactionAsync(
            branchId: ctx.Branch.Id,
            accountId: accountId ?? ctx.Account.Id,
            transactionTypeId: ctx.TransactionType.Id,
            categoryId: ctx.Category.Id,
            direction: ctx.Category.DefaultDirection,
            recordedByOperatorId: recordedByOperatorId ?? ctx.Operator.Id,
            createdByUserId: ctx.User.Id,
            date: date,
            value: value,
            description: description,
            transactionTime: transactionTime,
            clientId: clientId,
            status: status,
            createdAt: createdAt,
            id: id);
    }

    private static string QueryDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateTime Utc(int year, int month, int day, int hour)
    {
        return new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
    }

    private async Task<IReadOnlyList<Transaction>> ListInstallmentsAsync(Guid originId, Guid branchId)
    {
        using var scope = factory.Services.CreateScope();
        var transactionsRepository = scope.ServiceProvider.GetRequiredService<ITransactionsRepository>();
        var rows = await transactionsRepository.ListByOriginTransactionIdAndBranchIdAsNoTracking(originId, branchId);
        return rows.OrderBy(t => t.DueDate).ToList();
    }

    private sealed record LedgerContext(
        User User,
        Branch Branch,
        string Token,
        Operator Operator,
        Account Account,
        Category Category,
        TransactionType TransactionType);
}
