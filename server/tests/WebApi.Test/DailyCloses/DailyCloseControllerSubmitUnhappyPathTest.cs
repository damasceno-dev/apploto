using System.Net;
using server.Application.Services.DailyCloses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerSubmitUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Submit_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.PostAsync($"/dailyclose/{Guid.NewGuid()}/submit", content: null);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Submit_ShouldReturn409_WhenCloseIsLocked()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitLocked", Role.Manager);
        await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var closeDate = LocalToday();
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: closeDate,
            status: DailyCloseStatus.Draft);
        await factory.SeedSettingAsync(branch.Id, lockDate: closeDate);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
    }

    [Fact]
    public async Task Submit_ShouldReturn409_WhenCloseIsAlreadySubmitted()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitAlreadySubmitted", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_SUBMITTABLE);
    }

    [Fact]
    public async Task Submit_ShouldReturn403_WhenMemberSubmitsOlderDayClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcSubmitMemberOlder", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var olderDate = LocalToday().AddDays(-2);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: olderDate,
            status: DailyCloseStatus.Draft,
            submittedByOperatorId: op.Id);

        var httpResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
