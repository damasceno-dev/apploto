using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerGetHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ShouldReturnEntry_WhenManagerReadsAnyOperatorRow()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetManager", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-5);
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{entry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Id.ShouldBe(entry.Id);
        payload.OperatorId.ShouldBe(op.Id);
        payload.Segments.Count.ShouldBe(1);
        payload.IsInProgress.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_ShouldReturnEntry_WhenMemberReadsOwnOperatorRow()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetMemberOwn", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var date = SpLocalDate().AddDays(-3);
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{entry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Id.ShouldBe(entry.Id);
        payload.OperatorId.ShouldBe(op.Id);
    }

    [Fact]
    public async Task Get_ShouldRecomputeClosedRowUnderEntryDatePolicy_NotPersistedCheckpoint()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetClosed", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-2);
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);
        // Deliberately stale checkpoint: a policy change never rewrites persisted totals,
        // so the read must recompute — 08–17 under the seeded 7.33/1.00/0.25 policy is
        // 9 h gross − 1 h lunch = 8 h (+0.67), regardless of what the checkpoint says.
        await SetPersistedTotalsAsync(entry.Id, totalHours: 5m, balanceHours: -2.33m);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{entry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.TotalHours.ShouldBe(8m);
        payload.BalanceHours.ShouldBe(0.67m);
        payload.IsInProgress.ShouldBeFalse();

        // The persisted row still carries the checkpoint — reads recompute, never rewrite.
        var reload = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        reload.TotalHours.ShouldBe(5m);
        reload.BalanceHours.ShouldBe(-2.33m);
    }

    [Fact]
    public async Task Get_ShouldRecomputeLiveRunning_ForInProgressCurrentDayRow_AndDivergeFromPersistedSnapshot()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetLive", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var today = SpLocalDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, today,
            segments: [(today.AddHours(0), null, true)]);
        // Force a stale persisted snapshot: 0h. The live recompute should produce a
        // value > 0 because the open segment was clocked in at midnight branch-local.
        await SetPersistedTotalsAsync(entry.Id, totalHours: 0m, balanceHours: -7.33m);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{entry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.IsInProgress.ShouldBeTrue();
        payload.TotalHours.ShouldBeGreaterThan(0m);

        var reload = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        reload.TotalHours.ShouldBe(0m);
        reload.BalanceHours.ShouldBe(-7.33m);
        // Response diverges from persisted: persisted is the last-write checkpoint,
        // response is the live-recomputed view.
        payload.TotalHours.ShouldNotBe(reload.TotalHours);
    }

    [Fact]
    public async Task Get_ShouldRecomputeAndReportInProgress_ForForgottenPriorDayOpenWithCompletedSibling()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetForgotten", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-2);
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, date,
            segments:
            [
                (date.AddHours(8), date.AddHours(12), true),
                (date.AddHours(13), null, true)
            ]);
        await SetPersistedTotalsAsync(entry.Id, totalHours: 99m, balanceHours: 99m);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{entry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.IsInProgress.ShouldBeTrue();
        payload.Segments.Count.ShouldBe(2);
        // Open segment on a prior day contributes 0 hours; only completed segment (4h) counts.
        payload.TotalHours.ShouldBe(4m, tolerance: 0.001m);
    }

    private async Task SetPersistedTotalsAsync(Guid timeEntryId, decimal totalHours, decimal balanceHours)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var row = await dbContext.TimeEntries.SingleAsync(te => te.Id == timeEntryId);
        row.TotalHours = totalHours;
        row.BalanceHours = balanceHours;
        await dbContext.SaveChangesAsync();
    }

    private static DateTime SpLocalDate()
    {
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date, DateTimeKind.Unspecified);
    }
}
