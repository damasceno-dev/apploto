using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntrySegmentControllerUpdateUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UpdateSegment_ShouldReturn403_WhenMemberAttempts()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdate403", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var (entry, date) = await SeedPresentEntryAsync(branch.Id, op.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;

        var httpResponse = await _client.PutAuthAsync(
            $"/timeentry/segment/{segmentId}",
            ValidRequest(date),
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn404_WhenSegmentDoesNotExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdate404", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();

        var httpResponse = await _client.PutAuthAsync(
            $"/timeentry/segment/{Guid.NewGuid()}",
            ValidRequest(date),
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn404_WhenSegmentBelongsToAnotherBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("TESegmentUpdate404BranchA", Role.Admin);
        await factory.SeedSettingAsync(branchA.Id);
        var branchB = await factory.SeedBranchForOtherContextAsync("TESegmentUpdate404BranchB");
        await factory.SeedSettingAsync(branchB.Id);
        var opB = await factory.SeedOperatorAsync(branchB.Id);
        var (entryB, date) = await SeedPresentEntryAsync(branchB.Id, opB.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entryB.Id)).Segments.Single().Id;

        var httpResponse = await _client.PutAuthAsync(
            $"/timeentry/segment/{segmentId}",
            ValidRequest(date),
            tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn400_WhenUpdatedSegmentOverlapsSibling()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdateOverlap", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(12), true),
            (date.AddHours(13), date.AddHours(17), true));
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id))
            .Segments.Single(segment => segment.ClockIn == date.AddHours(13)).Id;
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(11))
            .WithClockOut(date.AddHours(17))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/timeentry/segment/{segmentId}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn400_WhenUpdatedSegmentIsOutOfDayBounds()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdateBounds", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var (entry, date) = await SeedPresentEntryAsync(branch.Id, op.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddDays(1))
            .WithClockOut(date.AddDays(1).AddHours(1))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/timeentry/segment/{segmentId}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn400_WhenUpdateWouldCreateMultipleOpenSegments()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdateOpen", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(12), true),
            (date.AddHours(13), date.AddHours(17), true),
            (date.AddHours(18), null, true));
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id))
            .Segments.Single(segment => segment.ClockIn == date.AddHours(13)).Id;
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(13))
            .WithClockOut(null)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/timeentry/segment/{segmentId}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn400_WhenClockOutIsBeforeClockIn()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdateClockOutBefore", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var (entry, date) = await SeedPresentEntryAsync(branch.Id, op.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(12))
            .WithClockOut(date.AddHours(11))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/timeentry/segment/{segmentId}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn400_WhenParentStatusIsNotPresent()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdateStatus", Role.Manager);
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
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;

        var httpResponse = await _client.PutAuthAsync($"/timeentry/segment/{segmentId}", ValidRequest(date), token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS);
    }

    [Fact]
    public async Task UpdateSegment_ShouldReturn409_WhenLockDateBlocksEntryDate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentUpdateLock", Role.Admin);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        await factory.SeedSettingAsync(branch.Id, lockDate: date);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var (entry, _) = await SeedPresentEntryAsync(branch.Id, op.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;

        var httpResponse = await _client.PutAuthAsync($"/timeentry/segment/{segmentId}", ValidRequest(date), token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
    }

    private async Task<(server.Domain.Entities.TimeEntry Entry, DateTime Date)> SeedPresentEntryAsync(
        Guid branchId,
        Guid operatorId)
    {
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branchId,
            operatorId,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(17), true));
        return (entry, date);
    }

    private static RequestUpdateTimeEntrySegmentJson ValidRequest(DateTime date)
    {
        return new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(8))
            .WithClockOut(date.AddHours(17))
            .Build();
    }
}
