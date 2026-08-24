using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
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

    // -------------------------------------------------------------------------
    // M7.7 Phase 7: a constants change writes the effective-dated policy ledger row
    // in the same commit; a second same-day change mutates that day's row in place.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldAppendPolicyEffectiveToday_AndMutateItOnSecondSameDayChange()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingPolicyLedger");
        var seeded = await factory.SeedSettingAsync(branch.Id);
        var localToday = SpLocalDate();

        var firstResponse = await _client.PutAuthAsync(
            "/setting",
            new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build(),
            token,
            seeded.Version);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await firstResponse.ReadContentAsync<ResponseSettingJson>();

        var policiesAfterFirst = await ListPoliciesAsync(branch.Id);
        policiesAfterFirst.Count.ShouldBe(2);
        policiesAfterFirst[0].EffectiveFrom.ShouldBe(DateTime.MinValue);
        policiesAfterFirst[1].EffectiveFrom.ShouldBe(localToday);
        policiesAfterFirst[1].DailyTargetHours.ShouldBe(8.0m);
        policiesAfterFirst[1].LunchDeductionOver6H.ShouldBe(seeded.LunchDeductionOver6H);
        policiesAfterFirst[1].LunchDeductionOver4H.ShouldBe(seeded.LunchDeductionOver4H);

        var secondResponse = await _client.PutAuthAsync(
            "/setting",
            new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver6H(2.0m).Build(),
            token,
            firstPayload.Version);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // No third row: the same-day row is updated in place (unique BranchId+EffectiveFrom),
        // keeping today's resolution unambiguous and older days untouched.
        var policiesAfterSecond = await ListPoliciesAsync(branch.Id);
        policiesAfterSecond.Count.ShouldBe(2);
        policiesAfterSecond[1].Id.ShouldBe(policiesAfterFirst[1].Id);
        policiesAfterSecond[1].EffectiveFrom.ShouldBe(localToday);
        policiesAfterSecond[1].DailyTargetHours.ShouldBe(8.0m);
        policiesAfterSecond[1].LunchDeductionOver6H.ShouldBe(2.0m);
        policiesAfterSecond[0].DailyTargetHours.ShouldBe(seeded.DailyTargetHours);
    }

    [Fact]
    public async Task Update_ShouldNotWritePolicy_WhenValuesAreUnchanged()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingPolicyNoop");
        var seeded = await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.PutAuthAsync(
            "/setting",
            new RequestUpdateSettingJsonBuilder()
                .WithDailyTargetHours(seeded.DailyTargetHours)
                .Build(),
            token,
            seeded.Version);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Only the seeded MinValue initial row exists — a value-identical PUT appends nothing.
        var policies = await ListPoliciesAsync(branch.Id);
        policies.Count.ShouldBe(1);
        policies[0].EffectiveFrom.ShouldBe(DateTime.MinValue);
    }

    private async Task<IReadOnlyList<TimeEntryPolicy>> ListPoliciesAsync(Guid branchId)
    {
        using var scope = factory.Services.CreateScope();
        var policiesRepository = scope.ServiceProvider.GetRequiredService<ITimeEntryPoliciesRepository>();
        return await policiesRepository.ListActiveByBranchIdAsNoTracking(branchId);
    }

    private static DateTime SpLocalDate()
    {
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date,
            DateTimeKind.Unspecified);
    }
}
