using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>
/// The first-transition opening (gap AA) under the blind count: a retired fee saved only on the prior counted close is
/// part of the server opening but not of the review rows. The attempt agrees with the official difference, so that
/// comparison cannot reveal the omission; the attempt's own items name every opening product, so a product present in
/// the attempt's opening and absent from the review rows identifies the incomplete table without client arithmetic.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerHistoricalOpeningTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PriorOnlyRetiredFee_ShouldBeNamedInTheAttemptItems_WhileTheReviewRowsOmitIt()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("Historical opening", Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var cash = await factory.SeedProductAsync(branch.Id, "Dinheiro", displayOrder: 10);
        var fee = await factory.SeedProductAsync(branch.Id, "Historical fee", displayOrder: 20, active: false);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 90);
        var prior = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: new DateTime(2026, 9, 14));
        await factory.SeedDailyCloseItemAsync(prior.Id, cash.Id, 1_000m);
        await factory.SeedDailyCloseItemAsync(prior.Id, fee.Id, 100m);
        var current = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: new DateTime(2026, 9, 15));
        await factory.SeedDailyCloseItemAsync(current.Id, cash.Id, 1_000m);

        var attempt = (await _client.ConferAsync(current.Id, token)).Attempt;
        var submit = await _client.SubmitSessionAsync(current.Id, token);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());

        var review = await (await _client.GetAuthAsync($"/session/{current.Id}/review", token))
            .ReadContentAsync<ResponseDailyCloseReviewJson>();
        review.Items.ShouldNotContain(item => item.ProductId == fee.Id);
        review.Items.Single(item => item.IsCashVarianceProduct).ClosingValue.ShouldBe(-100m);
        attempt.Difference.ShouldBe(-100m);

        // The attempt is the server's per-product opening: the omitted fee is named there with its saved value.
        attempt.Items.ShouldContain(item => item.ProductId == fee.Id && item.OpeningValue == 100m && item.CountedValue == null);
        attempt.Items.Where(item => item.OpeningValue != null).Select(item => item.ProductId)
            .Except(review.Items.Select(item => item.ProductId))
            .ShouldBe([fee.Id]);

        var history = await (await _client.GetAuthAsync($"/session/{current.Id}/history", token))
            .ReadContentAsync<ResponseDailyCloseHistoryJson>();
        history.CountAttempts!.Single(item => item.Id == review.Count.SubmittedAttemptId).Items
            .ShouldContain(item => item.ProductId == fee.Id && item.OpeningValue == 100m);
    }
}
