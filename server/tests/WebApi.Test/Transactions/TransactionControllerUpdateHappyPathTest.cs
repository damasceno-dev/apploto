using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

[Collection(ServerApiCollection.Name)]
public class TransactionControllerUpdateHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_ShouldReturn200AndPersistEditableFields()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateHappy", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var originalClient = await factory.SeedClientAsync(branch.Id, "Original Client");
        var updatedClient = await factory.SeedClientAsync(branch.Id, "Updated Client");
        var transactionDate = DateTime.UtcNow.Date;
        var originalPaidAt = transactionDate.AddDays(1).AddHours(9);
        var updatedPaidAt = transactionDate.AddDays(4).AddHours(16);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: transactionDate,
            description: "before update",
            transactionTime: new TimeOnly(8, 30),
            clientId: originalClient.Id,
            dueDate: transactionDate,
            paidAt: originalPaidAt);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("after update")
            .WithDueDate(transactionDate.AddDays(3))
            .WithPaidAt(updatedPaidAt)
            .WithClientId(updatedClient.Id)
            .WithTransactionTime(new TimeOnly(14, 45))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTransactionJson>();
        payload.Id.ShouldBe(transaction.Id);
        payload.Description.ShouldBe(request.Description);
        payload.DueDate.ShouldBe(request.DueDate);
        payload.PaidAt.ShouldBe(request.PaidAt);
        payload.ClientId.ShouldBe(request.ClientId);
        payload.TransactionTime.ShouldBe(request.TransactionTime);

        var persisted = await factory.ReloadAsync<Transaction>(transaction.Id);
        persisted.ShouldNotBeNull();
        persisted.Description.ShouldBe(request.Description);
        persisted.DueDate.ShouldBe(request.DueDate);
        persisted.PaidAt.ShouldBe(request.PaidAt);
        persisted.ClientId.ShouldBe(request.ClientId);
        persisted.TransactionTime.ShouldBe(request.TransactionTime);
    }

    [Fact]
    public async Task Update_ShouldPreserveNonEditableFields_WhenEditableSubsetChanges()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TxnUpdateRegression", Role.Manager);
        var operatorContext = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var originalClient = await factory.SeedClientAsync(branch.Id, "Regression Old Client");
        var updatedClient = await factory.SeedClientAsync(branch.Id, "Regression New Client");
        var transactionDate = DateTime.UtcNow.Date.AddDays(-2);
        var createdAt = Utc(2025, 4, 12, 11);
        var transaction = await factory.SeedTransactionAsync(
            branchId: branch.Id,
            accountId: account.Id,
            transactionTypeId: transactionType.Id,
            categoryId: category.Id,
            direction: category.DefaultDirection,
            recordedByOperatorId: operatorContext.Id,
            createdByUserId: user.Id,
            date: transactionDate,
            value: 100.00m,
            description: "immutable proof before",
            transactionTime: new TimeOnly(9, 10),
            clientId: originalClient.Id,
            dueDate: transactionDate,
            paidAt: transactionDate.AddDays(1).AddHours(8),
            createdAt: createdAt);
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("immutable proof after")
            .WithDueDate(transactionDate.AddDays(10))
            .WithPaidAt(transactionDate.AddDays(5).AddHours(17))
            .WithClientId(updatedClient.Id)
            .WithTransactionTime(new TimeOnly(16, 20))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/transaction/{transaction.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var persisted = await factory.ReloadAsync<Transaction>(transaction.Id);
        persisted.ShouldNotBeNull();

        persisted.Description.ShouldBe(request.Description);
        persisted.DueDate.ShouldBe(request.DueDate);
        persisted.PaidAt.ShouldBe(request.PaidAt);
        persisted.ClientId.ShouldBe(request.ClientId);
        persisted.TransactionTime.ShouldBe(request.TransactionTime);

        persisted.Value.ShouldBe(100.00m);
        persisted.Date.ShouldBe(transactionDate);
        persisted.TransactionTypeId.ShouldBe(transactionType.Id);
        persisted.AccountId.ShouldBe(account.Id);
        persisted.RecordedByOperatorId.ShouldBe(operatorContext.Id);
        persisted.Status.ShouldBe(TransactionStatus.Active);
        persisted.CategoryId.ShouldBe(category.Id);
        persisted.Direction.ShouldBe(category.DefaultDirection);
        persisted.CreatedByUserId.ShouldBe(user.Id);
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.CreatedAt.ShouldBe(createdAt);
    }

    private static DateTime Utc(int year, int month, int day, int hour)
    {
        return new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
    }
}
