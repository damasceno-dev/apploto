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
public class DailyCloseControllerRejectedCorrectionHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RejectedCorrection_ShouldAllowRecordingMemberToEditAndResubmitOnNextDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcRejectedNextDayCorrection",
            Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday().AddDays(-1),
            DailyCloseStatus.Rejected,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddDays(-1),
            rejectionReason: "corrigir contagem");

        var firstSave = await PutAsync(close.Id, close.Version, product.Id, 100m, token);
        firstSave.Status.ShouldBe(DailyCloseStatus.Draft);
        firstSave.RejectionReason.ShouldBe("corrigir contagem");

        var secondSave = await PutAsync(close.Id, firstSave.Version, product.Id, 150m, token);
        secondSave.Status.ShouldBe(DailyCloseStatus.Draft);
        secondSave.RejectionReason.ShouldBe("corrigir contagem");

        var submitResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submitted = await submitResponse.ReadContentAsync<ResponseDailyCloseJson>();
        submitted.Status.ShouldBe(DailyCloseStatus.Submitted);
        submitted.RejectionReason.ShouldBeNull();
        submitted.Items.Single(item => item.ProductId == cashVarianceProduct.Id).Value.ShouldBe(150m);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.RejectionReason.ShouldBeNull();
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public async Task RejectedCorrection_ShouldAllowElevatedRoleToCorrectOlderDay(Role role)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcRejectedOldCorrection{role}",
            role);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday().AddDays(-10),
            DailyCloseStatus.Rejected,
            rejectionReason: "corrigir");

        var corrected = await PutAsync(close.Id, close.Version, product.Id, 80m, token);

        corrected.Status.ShouldBe(DailyCloseStatus.Draft);
        corrected.RejectionReason.ShouldBe("corrigir");
    }

    private async Task<ResponseDailyCloseJson> PutAsync(
        Guid closeId,
        uint version,
        Guid productId,
        decimal value,
        string token)
    {
        var response = await _client.PutAuthAsync(
            $"/dailyclose/{closeId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = productId, Value = value }]
            },
            token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
