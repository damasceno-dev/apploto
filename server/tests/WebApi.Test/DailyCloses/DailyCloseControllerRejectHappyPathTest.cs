using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerRejectHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reject_ShouldReturn200AndPersistRejectedStateReasonAndAudit_WhenManagerRejectsSubmittedClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcRejectHappy", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));
        var request = new RequestRejectDailyCloseJson { RejectionReason = "test" };

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reject", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Rejected);
        payload.RejectionReason.ShouldBe(request.RejectionReason);
        payload.UpdatedAt.ShouldNotBeNull();
        payload.UpdatedByUserId.ShouldBe(user.Id);
        payload.ApprovedAt.ShouldBeNull();
        payload.ApprovedByUserId.ShouldBeNull();

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Rejected);
        persisted.RejectionReason.ShouldBe(request.RejectionReason);
        persisted.UpdatedAt.ShouldNotBeNull();
        persisted.UpdatedByUserId.ShouldBe(user.Id);
        persisted.ApprovedAt.ShouldBeNull();
        persisted.ApprovedByUserId.ShouldBeNull();
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
