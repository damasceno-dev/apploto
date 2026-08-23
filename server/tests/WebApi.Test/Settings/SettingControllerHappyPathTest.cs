using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Settings;

[Collection(ServerApiCollection.Name)]
public class SettingControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // GET /setting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn200WithSetting_WhenAdminRequests()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingGetAdmin");
        var seeded = await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync("/setting", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseSettingJson>();
        payload.Id.ShouldBe(seeded.Id);
        payload.BranchId.ShouldBe(branch.Id);
        payload.LockDate.ShouldBe(seeded.LockDate);
        payload.DailyTargetHours.ShouldBe(seeded.DailyTargetHours);
        payload.LunchDeductionOver6H.ShouldBe(seeded.LunchDeductionOver6H);
        payload.LunchDeductionOver4H.ShouldBe(seeded.LunchDeductionOver4H);
        payload.Version.ShouldBe(seeded.Version);
        httpResponse.Headers.ETag.ShouldNotBeNull();
        httpResponse.Headers.ETag.Tag.ShouldBe($"\"{payload.Version}\"");
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenMemberRequests()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingGetMember", Role.Member);
        await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync("/setting", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // PUT /setting
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn200AndPersistMutableChanges_WhenAdminUpdates()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingUpdateAdmin");
        var seeded = await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpdateSettingJsonBuilder()
            .WithDailyTargetHours(9.0m)
            .WithLunchDeductionOver6H(1.5m)
            .WithLunchDeductionOver4H(0.5m)
            .Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token, seeded.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseSettingJson>();
        payload.LockDate.ShouldBe(seeded.LockDate);
        payload.DailyTargetHours.ShouldBe(9.0m);
        payload.LunchDeductionOver6H.ShouldBe(1.5m);
        payload.LunchDeductionOver4H.ShouldBe(0.5m);

        var persisted = await factory.ReloadAsync<Setting>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.LockDate.ShouldBe(seeded.LockDate);
        persisted.DailyTargetHours.ShouldBe(9.0m);
        persisted.LunchDeductionOver6H.ShouldBe(1.5m);
        persisted.LunchDeductionOver4H.ShouldBe(0.5m);
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenManagerUpdates()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingUpdateManager", Role.Manager);
        var seeded = await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token, seeded.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_ShouldOnlyTouchSuppliedFields_WhenPartialUpdate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingPartialUpdate");
        var seeded = await factory.SeedSettingAsync(
            branch.Id,
            lunchDeductionOver6H: 1.0m,
            lunchDeductionOver4H: 0.25m,
            lockDate: DateTime.MinValue);
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(9.0m).Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token, seeded.Version);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var persisted = await factory.ReloadAsync<Setting>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.DailyTargetHours.ShouldBe(9.0m);
        persisted.LunchDeductionOver6H.ShouldBe(seeded.LunchDeductionOver6H);
        persisted.LunchDeductionOver4H.ShouldBe(seeded.LunchDeductionOver4H);
        persisted.LockDate.ShouldBe(seeded.LockDate);
    }

}
