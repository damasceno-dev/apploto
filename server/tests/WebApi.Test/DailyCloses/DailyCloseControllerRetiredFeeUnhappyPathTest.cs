using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerRetiredFeeUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetiredSavedFee_ReadsRemainVisible_SavePreviewReject_SubmitPreservesExistingRules(bool locked)
    {
        // Characterizes existing gap AA, including rows retired before the migration.
        // M95 itself refuses to create this trap on unlocked historical counts.
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("M95 retired fee", Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var fee = await factory.SeedProductAsync(branch.Id, "Renamed historical fee", active: false);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var date = new DateTime(2026, 9, 15);
        await factory.SeedSettingAsync(branch.Id, locked ? date : DateTime.MinValue);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: date);
        var item = await factory.SeedDailyCloseItemAsync(close.Id, fee.Id, 120m);
        var get = await _client.GetAuthAsync($"/session/{close.Id}", token);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await get.ReadContentAsync<ResponseDailyCloseJson>()).Items.ShouldContain(i => i.ProductId == fee.Id && i.Value == 120m);
        var review = await _client.GetAuthAsync($"/session/{close.Id}/review", token);
        review.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await review.ReadContentAsync<ResponseDailyCloseReviewJson>()).Items.ShouldContain(i => i.ProductId == fee.Id && i.ClosingValue == 120m);
        var items = new[] { new RequestUpsertDailyCloseItemJson { ProductId = fee.Id, Value = 120m } };
        var save = await _client.PutAuthAsync($"/session/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson { Version = close.Version, Items = items }, token);
        save.StatusCode.ShouldBe(locked ? HttpStatusCode.Conflict : HttpStatusCode.NotFound);
        if (!locked)
        {
            (await save.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_NOT_FOUND);
        }
        var unchanged = await factory.ReloadAsync<DailyClose>(close.Id);
        unchanged!.Version.ShouldBe(close.Version);
        var submit = await _client.SubmitSessionAsync(close.Id, token);
        submit.StatusCode.ShouldBe(locked ? HttpStatusCode.Conflict : HttpStatusCode.OK);
        var retained = await factory.ReloadAsync<DailyCloseItem>(item.Id);
        retained!.Active.ShouldBeTrue();
        retained.Value.ShouldBe(120m);
        if (!locked)
        {
            var submittedReview = await _client.GetAuthAsync($"/session/{close.Id}/review", token);
            (await submittedReview.ReadContentAsync<ResponseDailyCloseReviewJson>()).Items
                .Single(i => i.IsCashVarianceProduct).ClosingValue.ShouldBe(120m);
        }
    }
}
