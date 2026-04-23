using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
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
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
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
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
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
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
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
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
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

    private async Task<List<Transaction>> ListInstallmentsAsync(Guid originId, Guid branchId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        return await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.OriginTransactionId == originId &&
                transaction.BranchId == branchId)
            .OrderBy(transaction => transaction.DueDate)
            .ToListAsync();
    }
}
