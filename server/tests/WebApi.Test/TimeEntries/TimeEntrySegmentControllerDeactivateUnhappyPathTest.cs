using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntrySegmentControllerDeactivateUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DeactivateSegment_ShouldReturn403_WhenMemberAttempts()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentDelete403", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var (entry, _) = await SeedPresentEntryAsync(branch.Id, op.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/segment/{segmentId}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task DeactivateSegment_ShouldReturn404_WhenSegmentDoesNotExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentDelete404", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/segment/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
    }

    [Fact]
    public async Task DeactivateSegment_ShouldReturn404_WhenSegmentBelongsToAnotherBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("TESegmentDelete404BranchA", Role.Admin);
        await factory.SeedSettingAsync(branchA.Id);
        var branchB = await factory.SeedBranchForOtherContextAsync("TESegmentDelete404BranchB");
        await factory.SeedSettingAsync(branchB.Id);
        var opB = await factory.SeedOperatorAsync(branchB.Id);
        var (entryB, _) = await SeedPresentEntryAsync(branchB.Id, opB.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entryB.Id)).Segments.Single().Id;

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/segment/{segmentId}", tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
    }

    [Fact]
    public async Task DeactivateSegment_ShouldReturn404_WhenSegmentIsAlreadyInactive()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentDeleteInactive", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(17), false));
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/segment/{segmentId}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
    }

    [Fact]
    public async Task DeactivateSegment_ShouldReturn409_WhenLockDateBlocksEntryDate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TESegmentDeleteLock", Role.Admin);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        await factory.SeedSettingAsync(branch.Id, lockDate: date);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var (entry, _) = await SeedPresentEntryAsync(branch.Id, op.Id);
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/segment/{segmentId}", token);

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
}
