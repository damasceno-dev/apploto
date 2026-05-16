using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntrySegmentControllerAddHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<string, Role> ElevatedRoles => new()
    {
        { "Manager", Role.Manager },
        { "Admin", Role.Admin },
    };

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public async Task AddSegment_ShouldReturn201AndRecomputeParentTotals_WhenElevatedRoleSubmitsValidClocks(
        string label,
        Role role)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync($"TESegmentAdd{label}", role);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(12), true));
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(13))
            .WithClockOut(date.AddHours(17))
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/timeentry/{entry.Id}/segment", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        httpResponse.Headers.Location.ShouldNotBeNull();
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Id.ShouldBe(entry.Id);
        payload.Segments.Count.ShouldBe(2);
        payload.TotalHours.ShouldBe(8m, tolerance: 0.01m);
        payload.IsInProgress.ShouldBeFalse();

        var persisted = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        persisted.TotalHours.ShouldBe(8m, tolerance: 0.01m);
        persisted.Segments.Count(segment => segment.Active).ShouldBe(2);
        persisted.Segments.Where(segment => segment.Active)
            .ShouldContain(segment => segment.ClockIn == date.AddHours(13) && segment.ClockOut == date.AddHours(17));
    }
}
