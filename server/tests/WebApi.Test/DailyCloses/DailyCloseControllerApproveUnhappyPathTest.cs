using System.Net;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerApproveUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Approve_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.PostAsync($"/dailyclose/{Guid.NewGuid()}/approve", content: null);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Approve_ShouldReturn404_WhenCloseDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcApproveMissing", Role.Manager);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{Guid.NewGuid()}/approve", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task Approve_ShouldReturn403_WhenMemberApproves()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcApproveMember403", Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/approve", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.ApprovedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Approve_ShouldReturn409_WhenCloseIsNotSubmitted()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcApproveWrongState", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Draft);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/approve", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_APPROVABLE);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Draft);
        persisted.ApprovedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Approve_ShouldReturn409_WhenCloseIsLocked()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcApproveLocked", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var closeDate = LocalToday();
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate,
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));
        await factory.SeedSettingAsync(branch.Id, lockDate: closeDate);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/approve", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.ApprovedAt.ShouldBeNull();
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
