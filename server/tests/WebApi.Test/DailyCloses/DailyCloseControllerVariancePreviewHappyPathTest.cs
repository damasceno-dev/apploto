using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerVariancePreviewHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task VariancePreview_ShouldUseCandidateInsteadOfSavedDraftValues_AndPersistNothing()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewCandidate",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);
        var savedItem = await factory.SeedDailyCloseItemAsync(close.Id, product.Id, 10m);
        var before = await factory.ReloadAsync<DailyClose>(close.Id);
        before.ShouldNotBeNull();

        var response = await PreviewAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 250m }],
            token);

        response.CashVariance.ShouldBe(250m);
        var after = await factory.ReloadAsync<DailyClose>(close.Id);
        after.ShouldNotBeNull();
        after.Version.ShouldBe(before.Version);
        after.Status.ShouldBe(before.Status);
        after.UpdatedAt.ShouldBe(before.UpdatedAt);
        after.UpdatedByUserId.ShouldBe(before.UpdatedByUserId);
        after.Notes.ShouldBe(before.Notes);
        var persistedItems = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        persistedItems.ShouldHaveSingleItem().Id.ShouldBe(savedItem.Id);
        persistedItems.ShouldHaveSingleItem().Value.ShouldBe(10m);
    }

    [Fact]
    public async Task VariancePreview_ShouldMatchSubmittedVariance_WhenSameCandidatesAreSaved()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewParity",
            Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);
        var candidates =
            new[]
            {
                new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 175m }
            };

        var preview = await PreviewAsync(close.Id, candidates, token);
        var putResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new RequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items = candidates,
                Notes = "Conferido antes do envio"
            },
            token);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var submitResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submittedItems = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);

        submittedItems.Single(item => item.ProductId == cashVarianceProduct.Id).Value
            .ShouldBe(preview.CashVariance);
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public async Task VariancePreview_ShouldReturn200_ForElevatedRoles(Role role)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcVariancePreviewElevated{role}",
            role);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var response = await PreviewAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 25m }],
            token);

        response.CashVariance.ShouldBe(25m);
    }

    [Fact]
    public async Task VariancePreview_ShouldReturn200_ForRecordingMemberInScope()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewMember",
            Role.Member);
        var memberOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(memberOperator.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            submittedByOperatorId: memberOperator.Id);

        var response = await PreviewAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 80m }],
            token);

        response.CashVariance.ShouldBe(80m);
    }

    [Fact]
    public async Task VariancePreview_ShouldReturn200_WhenRejectedCloseCanBeResubmitted()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewRejected",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            status: DailyCloseStatus.Rejected);

        var response = await PreviewAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 35m }],
            token);

        response.CashVariance.ShouldBe(35m);
    }

    private async Task<ResponseDailyCloseVariancePreviewJson> PreviewAsync(
        Guid closeId,
        IReadOnlyList<RequestUpsertDailyCloseItemJson> items,
        string token)
    {
        var httpResponse = await _client.PostAuthAsync(
            $"/dailyclose/{closeId}/variance-preview",
            new RequestDailyCloseVariancePreviewJson { Items = items },
            token);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await httpResponse.ReadContentAsync<ResponseDailyCloseVariancePreviewJson>();
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
