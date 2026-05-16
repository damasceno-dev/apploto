using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntrySegmentControllerAddUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AddSegment_ShouldReturn403_WhenMemberAttempts()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAdd403", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(12), true));

        var httpResponse = await _client.PostAuthAsync(
            $"/timeentry/{entry.Id}/segment",
            ValidRequest(date),
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn404_WhenTimeEntryDoesNotExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAdd404", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();

        var httpResponse = await _client.PostAuthAsync(
            $"/timeentry/{Guid.NewGuid()}/segment",
            ValidRequest(date),
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn404_WhenTimeEntryBelongsToAnotherBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("TESegmentAdd404BranchA", Role.Admin);
        await factory.SeedSettingAsync(branchA.Id);
        var branchB = await factory.SeedBranchForOtherContextAsync("TESegmentAdd404BranchB");
        await factory.SeedSettingAsync(branchB.Id);
        var opB = await factory.SeedOperatorAsync(branchB.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entryB = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branchB.Id,
            opB.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(12), true));

        var httpResponse = await _client.PostAuthAsync(
            $"/timeentry/{entryB.Id}/segment",
            ValidRequest(date),
            tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn400_WhenSegmentOverlapsExisting()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAddOverlap", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var (entry, date) = await SeedPresentEntryAsync(branch.Id);

        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(11))
            .WithClockOut(date.AddHours(17))
            .Build();
        var httpResponse = await _client.PostAuthAsync($"/timeentry/{entry.Id}/segment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn400_WhenSegmentIsOutOfDayBounds()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAddBounds", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var (entry, date) = await SeedPresentEntryAsync(branch.Id);

        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddDays(1))
            .WithClockOut(date.AddDays(1).AddHours(1))
            .Build();
        var httpResponse = await _client.PostAuthAsync($"/timeentry/{entry.Id}/segment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn400_WhenOpenSegmentAlreadyExists()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAddOpen", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), null, true));
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(13))
            .WithClockOut(null)
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/timeentry/{entry.Id}/segment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn400_WhenClockOutIsBeforeClockIn()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAddClockOutBefore", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var (entry, date) = await SeedPresentEntryAsync(branch.Id);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(12))
            .WithClockOut(date.AddHours(11))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/timeentry/{entry.Id}/segment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn400_WhenParentStatusIsNotPresent()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAddStatus", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Vacation,
            (date.AddHours(8), date.AddHours(12), true));

        var httpResponse = await _client.PostAuthAsync($"/timeentry/{entry.Id}/segment", ValidRequest(date), token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS);
    }

    [Fact]
    public async Task AddSegment_ShouldReturn409_WhenLockDateBlocksEntryDate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAddLock", Role.Admin);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        await factory.SeedSettingAsync(branch.Id, lockDate: date);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(12), true));

        var httpResponse = await _client.PostAuthAsync($"/timeentry/{entry.Id}/segment", ValidRequest(date), token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
    }

    [Fact]
    public async Task AddSegment_ConcurrentOpenRace_ShouldReturnOpenSegmentConflict()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentAddOpenRace", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var (entry, date) = await SeedPresentEntryAsync(branch.Id);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(13))
            .WithClockOut(null)
            .Build();

        await InstallOpenSegmentDelayTriggerAsync();
        try
        {
            using var firstClient = factory.CreateClient();
            using var secondClient = factory.CreateClient();

            var responses = await Task.WhenAll(
                firstClient.PostAuthAsync($"/timeentry/{entry.Id}/segment", request, token),
                secondClient.PostAuthAsync($"/timeentry/{entry.Id}/segment", request, token));

            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            var payload = await conflict.ReadContentAsync<TestResponseErrorJson>();
            payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_OPEN_SEGMENT_CONFLICT);
        }
        finally
        {
            await DropOpenSegmentDelayTriggerAsync();
        }
    }

    private async Task<(server.Domain.Entities.TimeEntry Entry, DateTime Date)> SeedPresentEntryAsync(Guid branchId)
    {
        var op = await factory.SeedOperatorAsync(branchId);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branchId,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(12), true));
        return (entry, date);
    }

    private static RequestAddTimeEntrySegmentJson ValidRequest(DateTime date)
    {
        return new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(13))
            .WithClockOut(date.AddHours(17))
            .Build();
    }

    private async Task InstallOpenSegmentDelayTriggerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE OR REPLACE FUNCTION test_sleep_open_timeentry_segment()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW."ClockOut" IS NULL THEN
                    PERFORM pg_sleep(0.5);
                END IF;
                RETURN NEW;
            END;
            $$;

            DROP TRIGGER IF EXISTS test_sleep_open_timeentry_segment_trigger ON "TimeEntrySegments";

            CREATE TRIGGER test_sleep_open_timeentry_segment_trigger
            BEFORE INSERT ON "TimeEntrySegments"
            FOR EACH ROW
            EXECUTE FUNCTION test_sleep_open_timeentry_segment();
            """);
    }

    private async Task DropOpenSegmentDelayTriggerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DROP TRIGGER IF EXISTS test_sleep_open_timeentry_segment_trigger ON "TimeEntrySegments";
            DROP FUNCTION IF EXISTS test_sleep_open_timeentry_segment();
            """);
    }
}
