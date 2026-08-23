using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerListHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_ShouldReturnAllBranchEntries_ForManager()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListManagerSeesAll", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var opA = await factory.SeedOperatorAsync(branch.Id);
        var opB = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-5);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, opA.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, opB.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var payload = await GetListAsync("/timeentry", token);

        payload.Items.Count.ShouldBe(2);
        payload.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task List_ShouldFilterToOwnOperator_ForMemberWithLinkedOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListMemberOwn", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var ownOp = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var otherOp = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-3);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, ownOp.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, otherOp.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var payload = await GetListAsync("/timeentry", token);

        payload.Items.Count.ShouldBe(1);
        payload.Items[0].OperatorId.ShouldBe(ownOp.Id);
    }

    [Fact]
    public async Task List_ShouldFilterToCallerOperator_WhenMineIsTrue()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListMine", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var ownOp = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var otherOp = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-4);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, ownOp.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, otherOp.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var payload = await GetListAsync("/timeentry?Mine=true", token);

        payload.Items.Count.ShouldBe(1);
        payload.Items[0].OperatorId.ShouldBe(ownOp.Id);
    }

    [Fact]
    public async Task List_ShouldPaginateConsistently_AcrossPages()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListPaging", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var startDate = SpLocalDate().AddDays(-30);
        for (var index = 0; index < 7; index++)
        {
            var date = startDate.AddDays(index);
            await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
                factory, branch.Id, op.Id, date,
                segments: [(date.AddHours(8), date.AddHours(17), true)]);
        }

        var page1 = await GetListAsync("/timeentry?Page=1&PageSize=3", token);
        var page2 = await GetListAsync("/timeentry?Page=2&PageSize=3", token);
        var page3 = await GetListAsync("/timeentry?Page=3&PageSize=3", token);

        page1.Items.Count.ShouldBe(3);
        page1.TotalCount.ShouldBe(7);
        page1.TotalPages.ShouldBe(3);
        page1.HasNext.ShouldBeTrue();
        page1.HasPrevious.ShouldBeFalse();

        page2.Items.Count.ShouldBe(3);
        page2.TotalCount.ShouldBe(7);
        page2.HasNext.ShouldBeTrue();
        page2.HasPrevious.ShouldBeTrue();

        page3.Items.Count.ShouldBe(1);
        page3.TotalCount.ShouldBe(7);
        page3.HasNext.ShouldBeFalse();
        page3.HasPrevious.ShouldBeTrue();
    }

    [Fact]
    public async Task List_ShouldFilterByStatusAndDateRange()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListFilters", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var oldDate = SpLocalDate().AddDays(-10);
        var midDate = SpLocalDate().AddDays(-5);
        var freshDate = SpLocalDate().AddDays(-1);

        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, oldDate, TimeEntryStatus.Vacation);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, midDate, TimeEntryStatus.Present,
            segments: [(midDate.AddHours(8), midDate.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, freshDate, TimeEntryStatus.Present,
            segments: [(freshDate.AddHours(8), freshDate.AddHours(17), true)]);

        var statusPayload = await GetListAsync($"/timeentry?Status={(int)TimeEntryStatus.Vacation}", token);
        statusPayload.Items.Count.ShouldBe(1);
        statusPayload.Items[0].Status.ShouldBe(TimeEntryStatus.Vacation);

        var dateRangePayload = await GetListAsync(
            $"/timeentry?DateFrom={midDate:yyyy-MM-dd}&DateTo={freshDate:yyyy-MM-dd}",
            token);
        dateRangePayload.Items.Count.ShouldBe(2);
        dateRangePayload.Items.All(item => item.Date >= midDate && item.Date <= freshDate).ShouldBeTrue();
    }

    [Fact]
    public async Task List_ShouldOrderByDateDescending()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListOrder", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var oldest = SpLocalDate().AddDays(-7);
        var middle = SpLocalDate().AddDays(-3);
        var newest = SpLocalDate().AddDays(-1);

        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, oldest,
            segments: [(oldest.AddHours(8), oldest.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, newest,
            segments: [(newest.AddHours(8), newest.AddHours(17), true)]);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, middle,
            segments: [(middle.AddHours(8), middle.AddHours(17), true)]);

        var payload = await GetListAsync("/timeentry", token);

        payload.Items.Count.ShouldBe(3);
        payload.Items[0].Date.ShouldBe(newest);
        payload.Items[1].Date.ShouldBe(middle);
        payload.Items[2].Date.ShouldBe(oldest);
    }

    [Fact]
    public async Task List_ShouldRecomputeLiveRunning_ForInProgressCurrentDayItem()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListLive", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var today = SpLocalDate();
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, today,
            segments: [(today.AddHours(0), null, true)]);
        await SetPersistedTotalsAsync(entry.Id, totalHours: 0m, balanceHours: -7.33m);

        var payload = await GetListAsync("/timeentry", token);

        var item = payload.Items.Single(it => it.Id == entry.Id);
        item.IsInProgress.ShouldBeTrue();
        item.TotalHours.ShouldBeGreaterThan(0m);

        var reload = await TimeEntrySegmentTestHelpers.ReloadTimeEntryWithSegmentsAsync(factory, entry.Id);
        reload.TotalHours.ShouldBe(0m);
        item.TotalHours.ShouldNotBe(reload.TotalHours);
    }

    [Fact]
    public async Task List_ShouldFilterAndPrioritizeInProgressEntriesBeforePaging()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListInProgress", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var today = SpLocalDate();
        var openEntries = new List<Guid>();

        for (var index = 0; index < 3; index++)
        {
            var date = today.AddDays(-10 - index);
            var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
                factory,
                branch.Id,
                op.Id,
                date,
                segments: [(date.AddHours(8), null, true)]);
            openEntries.Add(entry.Id);
        }

        for (var index = 0; index < 5; index++)
        {
            var date = today.AddDays(-index);
            await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
                factory,
                branch.Id,
                op.Id,
                date,
                segments: [(date.AddHours(8), date.AddHours(17), true)]);
        }

        var firstFilteredPage = await GetListAsync(
            "/timeentry?IsInProgress=true&Page=1&PageSize=2",
            token);
        var secondFilteredPage = await GetListAsync(
            "/timeentry?IsInProgress=true&Page=2&PageSize=2",
            token);

        firstFilteredPage.TotalCount.ShouldBe(3);
        firstFilteredPage.Items.ShouldAllBe(item => item.IsInProgress);
        secondFilteredPage.TotalCount.ShouldBe(3);
        secondFilteredPage.Items.ShouldAllBe(item => item.IsInProgress);
        firstFilteredPage.Items.Concat(secondFilteredPage.Items)
            .Select(item => item.Id)
            .Order()
            .ShouldBe(openEntries.Order());

        var triagePage = await GetListAsync(
            "/timeentry?InProgressFirst=true&Page=1&PageSize=3",
            token);
        triagePage.Items.ShouldAllBe(item => item.IsInProgress);
    }

    private async Task<ResponseListTimeEntriesJson> GetListAsync(string uri, string token)
    {
        var response = await _client.GetAuthAsync(uri, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseListTimeEntriesJson>();
    }

    private async Task SetPersistedTotalsAsync(Guid timeEntryId, decimal totalHours, decimal balanceHours)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
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
