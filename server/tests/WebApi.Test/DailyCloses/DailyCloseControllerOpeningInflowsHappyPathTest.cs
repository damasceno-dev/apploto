using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerOpeningInflowsHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UserEnteredSeededInflows_ShouldPreviewSaveAndSubmitExactlyOnce_WithoutFeeCount()
    {
        var user = await factory.SeedUserAsync();
        var branch = await factory.SeedConfiguredBranchAsync($"M95 {Guid.NewGuid():N}");
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Admin);
        var token = factory.IssueBranchToken(membership);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var date = new DateTime(2026, 9, 15);
        using var scope = factory.Services.CreateScope();
        var products = await scope.ServiceProvider.GetRequiredService<IProductsRepository>().ListActiveByBranchIdAsNoTracking(branch.Id);
        var cash = products.Single(p => p.Name == "Dinheiro");
        var opening = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: date.AddDays(-1));
        await factory.SeedDailyCloseItemAsync(opening.Id, cash.Id, 1_000m);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: date);
        var types = await scope.ServiceProvider.GetRequiredService<ITransactionTypesRepository>().ListActiveByBranchIdAsNoTracking(branch.Id);
        foreach (var (name, value) in new[] { ("Tarifa Bolão", 120m), ("Operador", 350m) })
        {
            var request = new RequestCreateTransactionJsonBuilder().WithDate(date).WithValue(value)
                .WithAccountId(account.Id).WithTransactionTypeId(types.Single(t => t.Name == name).Id)
                .WithRecordedByOperatorId(op.Id).WithTransactionTime(null).Build();
            var key = $"m95-{Guid.NewGuid():N}";
            var created = await _client.PostAuthAsync("/transaction", request, token, key);
            created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var payload = await created.ReadContentAsync<ResponseCreateTransactionJson>();
            var detailResponse = await _client.GetAuthAsync($"/transaction/{payload.Id}", token);
            var detail = await detailResponse.ReadContentAsync<ResponseTransactionJson>();
            detail.Direction.ShouldBe(Direction.In);
            detail.ClientId.ShouldBeNull();
            var replay = await _client.PostAuthAsync("/transaction", request, token, key);
            replay.StatusCode.ShouldBe(HttpStatusCode.Created);
            (await replay.ReadContentAsync<ResponseCreateTransactionJson>()).Id.ShouldBe(payload.Id);
        }
        var items = new[] { new RequestUpsertDailyCloseItemJson { ProductId = cash.Id, Value = 1_470m } };
        var preview = await _client.PostAuthAsync($"/session/{close.Id}/variance-preview",
            new RequestDailyCloseVariancePreviewJson { Items = items }, token);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await preview.ReadContentAsync<ResponseDailyCloseVariancePreviewJson>()).CashVariance.ShouldBe(0m);
        var save = await _client.PutAuthAsync($"/session/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson { Version = close.Version, Items = items }, token);
        save.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submit = await _client.PostAuthAsync($"/session/{close.Id}/submit", token);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var review = await _client.GetAuthAsync($"/session/{close.Id}/review", token);
        var reviewed = await review.ReadContentAsync<ResponseDailyCloseReviewJson>();
        reviewed.Items.Single(i => i.IsCashVarianceProduct).ClosingValue.ShouldBe(0m);
        reviewed.Items.ShouldNotContain(i => i.ProductName == "Tarifa Bolão");
    }
}
