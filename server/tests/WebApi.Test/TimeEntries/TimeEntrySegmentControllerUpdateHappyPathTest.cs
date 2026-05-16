using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntrySegmentControllerUpdateHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<string, Role> ElevatedRoles => new()
    {
        { "Manager", Role.Manager },
        { "Admin", Role.Admin },
    };

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public async Task UpdateSegment_ShouldReturn200AndRecomputeParentTotals_WhenElevatedRoleSubmitsValidClocks(
        string label,
        Role role)
    {
        var (_, branch, membership, token) = await factory.SeedFullBranchContextAsync($"TESegmentUpdate{label}", role);
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
        var persisted = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        var segmentId = persisted.Segments.Single(segment => segment.ClockIn == date.AddHours(13)).Id;
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(12.5))
            .WithClockOut(date.AddHours(17.5))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/timeentry/segment/{segmentId}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Id.ShouldBe(entry.Id);
        payload.Segments.Count.ShouldBe(2);
        payload.Segments.Single(segment => segment.Id == segmentId).ClockIn.ShouldBe(date.AddHours(12.5));
        payload.Segments.Single(segment => segment.Id == segmentId).ClockOut.ShouldBe(date.AddHours(17.5));
        payload.TotalHours.ShouldBe(8.5m, tolerance: 0.01m);

        var reloadedSegment = await TimeEntrySegmentTestHelpers.ReloadSegmentAsync(factory, segmentId);
        reloadedSegment.ClockIn.ShouldBe(date.AddHours(12.5));
        reloadedSegment.ClockOut.ShouldBe(date.AddHours(17.5));
        reloadedSegment.UpdatedByUserId.ShouldBe(membership.UserId);

        var reloadedParent = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        reloadedParent.TotalHours.ShouldBe(8.5m, tolerance: 0.01m);
        reloadedParent.UpdatedByUserId.ShouldBe(membership.UserId);
    }
}
