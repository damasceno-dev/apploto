using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.TimeEntries;
using Xunit;

namespace WebApi.Test.Settings;

/// <summary>
/// Regression test: updating LunchDeductionOver6H via PUT /setting immediately
/// affects the live-recomputed TotalHours returned by GET /timeentry/{id} for an
/// in-progress current-day row. Proves the M5 read path loads the Setting fresh
/// on every request — no cache, no event needed.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SettingControllerTierRegressionTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task UpdateLunchDeduction_ShouldImmediatelyAffect_LiveRecomputedTotalHours()
    {
        // Fix the branch clock at noon so the open segment is deterministically in the past
        // and every value (ClockIn, entryDate, branchLocalNow) is on the same local day.
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var today = DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date,
            DateTimeKind.Unspecified);
        var fixedLocalNow = today.AddHours(12); // fixed noon SP

        await using var clockFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBranchClock>();
                services.AddSingleton<IBranchClock>(new FixedNoonBranchClock(fixedLocalNow));
            });
        });
        var client = clockFactory.CreateClient();

        // Arrange: branch with setting using LunchDeductionOver6H = 1.0
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("SettingTierRegression");
        var setting = await factory.SeedSettingAsync(branch.Id, lunchDeductionOver6H: 1.0m);
        var op = await factory.SeedOperatorAsync(branch.Id);

        // ClockIn = today 04:00 — within [today, today + 1 day) per loto-specs invariant.
        // branchLocalNow is fixed at noon: gross = 12:00 − 04:00 = 8 h > 6 h → tier applies.
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, today,
            segments: [(today.AddHours(4), null, true)]);

        // Act 1: GET timeentry — capture TotalHours with LunchDeductionOver6H = 1.0
        var firstGetResponse = await client.GetAuthAsync($"/timeentry/{entry.Id}", token);
        firstGetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await firstGetResponse.ReadContentAsync<ResponseTimeEntryJson>();
        firstPayload.IsInProgress.ShouldBeTrue();
        firstPayload.TotalHours.ShouldBeGreaterThan(0m); // 8h gross − 1h = 7h

        // Act 2: increase LunchDeductionOver6H from 1.0 to 2.0 via PUT /setting
        var updateRequest = new RequestUpdateSettingJsonBuilder()
            .WithLunchDeductionOver6H(2.0m)
            .Build();
        var updateResponse = await client.PutAuthAsync("/setting", updateRequest, token, setting.Version);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act 3: GET same timeentry — TotalHours must now reflect the new (larger) deduction.
        // Gross is fixed (clock is frozen), so second total is exactly 1h less than first.
        var secondGetResponse = await client.GetAuthAsync($"/timeentry/{entry.Id}", token);
        secondGetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPayload = await secondGetResponse.ReadContentAsync<ResponseTimeEntryJson>();
        secondPayload.IsInProgress.ShouldBeTrue();
        secondPayload.TotalHours.ShouldBeLessThan(firstPayload.TotalHours);
    }

    private sealed class FixedNoonBranchClock(DateTime fixedLocalNow) : IBranchClock
    {
        public DateTime UtcNow() => DateTime.UtcNow;
        public DateTime LocalBusinessDateTime(DateTime utcInstant) => fixedLocalNow;
        public DateTime LocalBusinessDate(DateTime utcInstant) => fixedLocalNow.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant) =>
            localBusinessDate.Date == fixedLocalNow.Date;
    }
}
