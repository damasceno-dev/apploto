using System.Net;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerApproveHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Approve_ShouldReturn200AndPersistApprovedStateAndAudit_WhenManagerApprovesSubmittedClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcApproveHappy", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/approve", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Approved);
        payload.ApprovedAt.ShouldNotBeNull();
        payload.UpdatedAt.ShouldNotBeNull();
        payload.ApprovedAt.ShouldBe(payload.UpdatedAt);
        payload.ApprovedByUserId.ShouldBe(user.Id);
        payload.UpdatedByUserId.ShouldBe(user.Id);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Approved);
        persisted.ApprovedAt.ShouldNotBeNull();
        persisted.UpdatedAt.ShouldNotBeNull();
        persisted.ApprovedAt.ShouldBe(persisted.UpdatedAt);
        persisted.ApprovedByUserId.ShouldBe(user.Id);
        persisted.UpdatedByUserId.ShouldBe(user.Id);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
