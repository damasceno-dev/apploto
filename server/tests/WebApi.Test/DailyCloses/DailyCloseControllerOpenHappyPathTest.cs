using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerOpenHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Open_ShouldReturn201AndPersistDraft_WhenManagerOpensForLinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenMgr", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday())
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.AccountId.ShouldBe(account.Id);
        payload.AccountName.ShouldBe(account.Name);
        payload.BranchId.ShouldBe(branch.Id);
        payload.OpenedByUserId.ShouldBe(user.Id);
        payload.RecordedByUserId.ShouldBeNull();
        payload.RecordedByOperatorId.ShouldBeNull();
        payload.SubmittedByUserId.ShouldBeNull();
        payload.SubmittedByOperatorId.ShouldBeNull();
        payload.SubmittedAt.ShouldBeNull();
        payload.Items.ShouldBeEmpty();

        var persisted = await factory.ReloadAsync<DailyClose>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.AccountId.ShouldBe(account.Id);
        persisted.Date.ShouldBe(request.Date);
        persisted.Status.ShouldBe(DailyCloseStatus.Draft);
        persisted.OpenedByUserId.ShouldBe(user.Id);
        persisted.RecordedByUserId.ShouldBeNull();
        persisted.RecordedByOperatorId.ShouldBeNull();
        persisted.SubmittedByUserId.ShouldBeNull();
        persisted.SubmittedByOperatorId.ShouldBeNull();
    }

    [Fact]
    public async Task Open_ShouldReturn201WithNullOperatorId_WhenManagerHasNoLinkedOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenMgrNoOp", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday())
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.OpenedByUserId.ShouldBe(user.Id);
        payload.RecordedByUserId.ShouldBeNull();
        payload.SubmittedByOperatorId.ShouldBeNull();

        var persisted = await factory.ReloadAsync<DailyClose>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.OpenedByUserId.ShouldBe(user.Id);
        persisted.RecordedByUserId.ShouldBeNull();
        persisted.SubmittedByOperatorId.ShouldBeNull();
    }

    [Fact]
    public async Task Open_ShouldReturn201_WhenMemberOpensForLinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenMember", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday())
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.OpenedByUserId.ShouldBe(user.Id);
        payload.RecordedByOperatorId.ShouldBeNull();
        payload.SubmittedByOperatorId.ShouldBeNull();

        var persisted = await factory.ReloadAsync<DailyClose>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.OpenedByUserId.ShouldBe(user.Id);
        persisted.RecordedByOperatorId.ShouldBeNull();
        persisted.SubmittedByOperatorId.ShouldBeNull();
    }

    [Fact]
    public async Task Open_ShouldReturn201_WhenManagerBackdatesTerminalCloseBeforeLock()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcOpenMgrBackdate",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var targetDate = LocalToday().AddDays(-10);
        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(targetDate)
            .WithAccountId(account.Id)
            .Build();

        var response = await _client.PostAuthAsync("/dailyclose", request, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await response.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Date.ShouldBe(targetDate);
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
