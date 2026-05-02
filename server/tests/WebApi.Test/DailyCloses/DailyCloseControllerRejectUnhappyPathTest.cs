using System.Net;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerRejectUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reject_ShouldReturn403_WhenMemberRejects()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcRejectMember403", Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));
        var request = new RequestRejectDailyCloseJson { RejectionReason = "test" };

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reject", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public async Task Reject_ShouldReturn409_WhenCloseIsNotSubmitted()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcRejectWrongState", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Rejected);
        var request = new RequestRejectDailyCloseJson { RejectionReason = "test" };

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reject", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_REJECTABLE);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Rejected);
        persisted.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public async Task Reject_ShouldReturn409_WhenCloseIsLocked()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcRejectLocked", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var closeDate = LocalToday();
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate,
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));
        await factory.SeedSettingAsync(branch.Id, lockDate: closeDate);
        var request = new RequestRejectDailyCloseJson { RejectionReason = "test" };

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reject", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.RejectionReason.ShouldBeNull();
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
