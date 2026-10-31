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
    public async Task SeededInflows_ShouldBeEnteredOnceThroughTheCloseFields_WithoutFeeCount()
    {
        // M9.6 Phase 7 supersedes the M9.5 manual rows: Tarifa Bolão and the terminal report (formerly Operador) are
        // close-form fields whose generated rows explain the cash; the generic create refuses their system types.
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
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: date, custodianOperatorId: op.Id);
        var types = await scope.ServiceProvider.GetRequiredService<ITransactionTypesRepository>().ListActiveByBranchIdAsNoTracking(branch.Id);
        foreach (var name in new[] { "Tarifa Bolão", "Relatório do terminal" })
        {
            var request = new RequestCreateTransactionJsonBuilder().WithDate(date).WithValue(10m)
                .WithAccountId(account.Id).WithTransactionTypeId(types.Single(t => t.Name == name).Id)
                .WithRecordedByOperatorId(op.Id).WithTransactionTime(null).Build();
            var refused = await _client.PostAuthAsync("/transaction", request, token, $"m96-{Guid.NewGuid():N}");
            refused.StatusCode.ShouldBe(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync());
        }

        var items = new[] { new RequestUpsertDailyCloseItemJson { ProductId = cash.Id, Value = 1_470m } };
        var fields = new[]
        {
            new RequestSessionCloseFieldJson { Field = SessionCloseField.BolaoFee, Value = 120m },
            new RequestSessionCloseFieldJson { Field = SessionCloseField.TerminalReport, Value = 350m }
        };
        var current = await _client.TryGetSessionAsync(close.Id, token);
        var save = await _client.PutAuthAsync($"/session/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson { Version = current!.Version, Items = items, CloseFields = fields }, token);
        save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
        var replayed = await _client.PutAuthAsync($"/session/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = (await save.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose.Version,
                Items = items,
                CloseFields = fields
            },
            token);
        replayed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.ConferAsync(close.Id, token)).Attempt.Difference.ShouldBe(0m);
        var submit = await _client.SubmitSessionAsync(close.Id, token);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var review = await _client.GetAuthAsync($"/session/{close.Id}/review", token);
        var reviewed = await review.ReadContentAsync<ResponseDailyCloseReviewJson>();
        reviewed.Items.Single(i => i.IsCashVarianceProduct).ClosingValue.ShouldBe(0m);
        reviewed.Items.ShouldNotContain(i => i.ProductName == "Tarifa Bolão");
        reviewed.CloseFields.Where(f => f.Value != 0m).Select(f => (f.Field, f.Value, f.Revision))
            .ShouldBe([(SessionCloseField.TerminalReport, 350m, 1), (SessionCloseField.BolaoFee, 120m, 1)]);
    }
}
