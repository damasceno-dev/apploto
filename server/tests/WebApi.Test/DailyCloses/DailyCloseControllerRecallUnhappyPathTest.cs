using System.Net;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerRecallUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Recall_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await _client.PostAsync($"/dailyclose/{Guid.NewGuid()}/recall", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Recall_ShouldReturn404_WhenCloseBelongsToAnotherBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync(
            "DcRecallCrossBranch",
            Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DcRecallCrossBranchOther");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var otherClose = await factory.SeedDailyCloseAsync(
            otherBranch.Id,
            otherAccount.Id,
            LocalToday(),
            DailyCloseStatus.Submitted);

        var response = await _client.PostAuthAsync(
            $"/dailyclose/{otherClose.Id}/recall",
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task Recall_ShouldReturn403_WhenMemberIsNotRecordingOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcRecallOtherOperator",
            Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var recordingOperator = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday(),
            DailyCloseStatus.Submitted,
            submittedByOperatorId: recordingOperator.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/recall", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(
            ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
    }

    [Fact]
    public async Task Recall_ShouldReturn403AndPreserveSubmittedState_WhenRecordingMemberTargetsOlderDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcRecallMemberOld",
            Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday().AddDays(-1),
            DailyCloseStatus.Submitted,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddDays(-1));

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/recall", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_RECALL_REQUIRES_SAME_DAY);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.SubmittedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(DailyCloseStatus.Draft)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task Recall_ShouldReturn409_WhenCloseIsNotSubmitted(DailyCloseStatus status)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcRecallState{status}",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, LocalToday(), status);

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/recall", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_RECALLABLE);
    }

    [Fact]
    public async Task Recall_ShouldReturn409_WhenCloseDateIsLocked()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcRecallLocked",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var date = LocalToday().AddDays(-2);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Submitted,
            submittedAt: DateTime.UtcNow.AddDays(-2));
        await factory.SeedSettingAsync(branch.Id, lockDate: date);

        var response = await _client.PostAuthAsync($"/dailyclose/{close.Id}/recall", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
