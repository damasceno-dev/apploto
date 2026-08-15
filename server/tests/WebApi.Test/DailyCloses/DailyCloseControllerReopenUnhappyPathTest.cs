using System.Net;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerReopenUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reopen_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await _client.PostAsync($"/dailyclose/{Guid.NewGuid()}/reopen", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Reopen_ShouldReturn404_WhenCloseBelongsToAnotherBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync(
            "DcReopenCrossBranch",
            Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DcReopenCrossBranchOther");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var otherClose = await factory.SeedDailyCloseAsync(
            otherBranch.Id,
            otherAccount.Id,
            LocalToday(),
            DailyCloseStatus.Approved);

        var response = await _client.PostAuthAsync(
            $"/dailyclose/{otherClose.Id}/reopen",
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task Reopen_ShouldReturn403AndPreserveApprovedState_WhenMemberCallsEndpoint()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcReopenMember",
            Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday(),
            DailyCloseStatus.Approved,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-10),
            approvedAt: DateTime.UtcNow.AddMinutes(-5));

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reopen", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Approved);
        persisted.SubmittedAt.ShouldNotBeNull();
        persisted.ApprovedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Reopen_ShouldReturn409AndPreserveApprovedState_WhenPeriodIsLocked()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcReopenLocked",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var date = LocalToday().AddDays(-2);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Approved,
            submittedAt: DateTime.UtcNow.AddDays(-2),
            approvedAt: DateTime.UtcNow.AddDays(-1));
        await factory.SeedSettingAsync(branch.Id, lockDate: date);

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reopen", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Approved);
        persisted.SubmittedAt.ShouldNotBeNull();
        persisted.ApprovedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(DailyCloseStatus.Draft)]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task Reopen_ShouldReturn409_WhenCloseIsNotApproved(DailyCloseStatus status)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcReopenState{status}",
            Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, LocalToday(), status);

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reopen", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_REOPENABLE);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
