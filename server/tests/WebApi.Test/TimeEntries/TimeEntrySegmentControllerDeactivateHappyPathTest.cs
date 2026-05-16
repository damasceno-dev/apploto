using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntrySegmentControllerDeactivateHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<string, Role> ElevatedRoles => new()
    {
        { "Manager", Role.Manager },
        { "Admin", Role.Admin },
    };

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public async Task DeactivateSegment_ShouldReturn200SoftDeleteRowAndRecomputeParent_WhenElevatedRoleSubmitsValidId(
        string label,
        Role role)
    {
        var (_, branch, membership, token) = await factory.SeedFullBranchContextAsync($"TESegmentDelete{label}", role);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = TimeEntrySegmentTestHelpers.FixedDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory,
            branch.Id,
            op.Id,
            date,
            TimeEntryStatus.Present,
            (date.AddHours(8), date.AddHours(17), true));
        var segmentId = (await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id)).Segments.Single().Id;

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/segment/{segmentId}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Id.ShouldBe(entry.Id);
        payload.Segments.ShouldBeEmpty();
        payload.IsInProgress.ShouldBeFalse();
        payload.TotalHours.ShouldBe(0m);
        payload.BalanceHours.ShouldBe(-7.33m);

        var reloadedSegment = await TimeEntrySegmentTestHelpers.ReloadSegmentAsync(factory, segmentId);
        reloadedSegment.Active.ShouldBeFalse();
        reloadedSegment.UpdatedByUserId.ShouldBe(membership.UserId);

        var reloadedParent = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        reloadedParent.TotalHours.ShouldBe(0m);
        reloadedParent.BalanceHours.ShouldBe(-7.33m);
        reloadedParent.UpdatedByUserId.ShouldBe(membership.UserId);
    }
}
