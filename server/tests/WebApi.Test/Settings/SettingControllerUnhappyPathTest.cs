using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Settings;

[Collection(ServerApiCollection.Name)]
public class SettingControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // Auth checks
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn401_WhenNoToken()
    {
        var httpResponse = await _client.GetAsync("/setting");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn401_WhenNoToken()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build();

        var httpResponse = await _client.PutAsync("/setting", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenMemberTriesToUpdate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingUpdateMember403", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    // -------------------------------------------------------------------------
    // 400 validations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn400_WhenDailyTargetHoursIsZero()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingDailyTargetZero");
        await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(0m).Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenDailyTargetHoursExceeds24()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingDailyTargetMax");
        await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(25m).Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenLunchDeductionOver6HIsNegative()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingLunch6HNeg");
        await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver6H(-0.01m).Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenLunchDeductionOver4HExceeds8()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingLunch4HMax");
        await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver4H(9m).Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }

    [Fact]
    public async Task Update_ShouldReturn400AndNotMutate_WhenLockDateIsProvided()
    {
        var lockDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingLockBackward");
        var seeded = await factory.SeedSettingAsync(branch.Id, lockDate: lockDate);
        var request = new RequestUpdateSettingJsonBuilder()
            .WithLockDate(lockDate.AddDays(-1))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/setting", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_LOCK_DATE_READ_ONLY);
        var persisted = await factory.ReloadAsync<server.Domain.Entities.Setting>(seeded.Id);
        persisted.ShouldNotBeNull();
        persisted.LockDate.ShouldBe(DateTime.SpecifyKind(lockDate, DateTimeKind.Unspecified));
    }
}
