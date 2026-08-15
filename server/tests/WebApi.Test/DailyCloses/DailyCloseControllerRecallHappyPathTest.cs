using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerRecallHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Recall_ShouldReturn200AndPreserveCashVariance_WhenRecordingMemberRecallsSameDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcRecallMemberSameDay",
            Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday(),
            DailyCloseStatus.Submitted,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-10),
            notes: "snapshot note");
        var cashVariance = await factory.SeedDailyCloseItemAsync(
            close.Id,
            cashVarianceProduct.Id,
            45m);

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/recall", token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.SubmittedAt.ShouldBeNull();
        payload.UpdatedByUserId.ShouldBe(user.Id);
        payload.UpdatedAt.ShouldNotBeNull();
        payload.Notes.ShouldBe("snapshot note");
        payload.Items.ShouldNotContain(item => item.ProductId == cashVarianceProduct.Id);
        payload.ItemsFirstRecordedAt.ShouldNotBeNull();

        var getResponse = await _client.GetAuthAsync($"/dailyclose/{close.Id}", token);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getPayload = await getResponse.ReadContentAsync<ResponseDailyCloseJson>();
        getPayload.Items.ShouldNotContain(item => item.ProductId == cashVarianceProduct.Id);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Draft);
        persisted.SubmittedAt.ShouldBeNull();
        persisted.UpdatedByUserId.ShouldBe(user.Id);
        persisted.ItemsFirstRecordedAt.ShouldNotBeNull();
        var retainedVariance = await factory.ReloadAsync<DailyCloseItem>(cashVariance.Id);
        retainedVariance.ShouldNotBeNull();
        retainedVariance.Active.ShouldBeTrue();
        retainedVariance.Value.ShouldBe(45m);
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public async Task Recall_ShouldReturn200_WhenElevatedRoleRecallsOlderSubmittedClose(Role role)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcRecallOld{role}",
            role);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday().AddDays(-5),
            DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddDays(-5));

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/recall", token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.SubmittedAt.ShouldBeNull();
        payload.UpdatedByUserId.ShouldBe(user.Id);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
