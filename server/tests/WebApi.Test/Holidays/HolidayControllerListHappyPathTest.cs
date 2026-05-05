using System.Globalization;
using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerListHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_ShouldReturnOnlyAuthenticatedBranchRows()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("HolListBranchA");
        var (_, branchB, _, _) = await factory.SeedFullBranchContextAsync("HolListBranchB");
        var holidayA = await factory.SeedHolidayAsync(branchA.Id, new DateTime(2025, 9, 7), "Independência");
        await factory.SeedHolidayAsync(branchB.Id, new DateTime(2025, 9, 7), "Independência");

        var payload = await GetListAsync("/holiday", tokenA);

        payload.Items.Select(h => h.Id).ShouldContain(holidayA.Id);
        payload.Items.All(h => h.BranchId == branchA.Id).ShouldBeTrue();
        payload.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_ShouldReturn200_WhenMemberRequests()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolListMember", Role.Member);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 11, 2), "Finados");

        var payload = await GetListAsync("/holiday", token);

        payload.Items.Select(h => h.Id).ShouldContain(holiday.Id);
    }

    [Fact]
    public async Task List_ShouldApplyYearFilter()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolListYear");
        var match2025 = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 9, 7), "Independência");
        await factory.SeedHolidayAsync(branch.Id, new DateTime(2026, 1, 1), "Ano Novo 2026");

        var payload = await GetListAsync("/holiday?year=2025", token);

        payload.Items.Select(h => h.Id).ShouldContain(match2025.Id);
        payload.Items.All(h => h.Date.Year == 2025).ShouldBeTrue();
    }

    [Fact]
    public async Task List_ShouldApplyDateRangeFilter()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolListDateRange");
        var inside = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 9, 7), "Independência");
        var outside = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 1, 1), "Confraternização");

        var dateFrom = QueryDate(new DateTime(2025, 9, 1));
        var dateTo = QueryDate(new DateTime(2025, 9, 30));
        var payload = await GetListAsync($"/holiday?dateFrom={dateFrom}&dateTo={dateTo}", token);

        payload.Items.Select(h => h.Id).ShouldContain(inside.Id);
        payload.Items.Select(h => h.Id).ShouldNotContain(outside.Id);
    }

    [Fact]
    public async Task List_ShouldReturnEmptyItems_WhenBranchHasNoHolidays()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolListEmpty");

        var payload = await GetListAsync("/holiday", token);

        payload.Items.ShouldBeEmpty();
        payload.TotalCount.ShouldBe(0);
        payload.TotalPages.ShouldBe(0);
        payload.HasNext.ShouldBeFalse();
        payload.HasPrevious.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, 3, true, false)]
    [InlineData(2, 3, true, true)]
    [InlineData(3, 1, false, true)]
    public async Task List_ShouldPageSevenRowsWithPageSizeThreeAndExposePagingMetadata(
        int page,
        int expectedItemCount,
        bool expectedHasNext,
        bool expectedHasPrevious)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync($"HolListPagP{page}");

        foreach (var index in Enumerable.Range(1, 7))
        {
            await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, index <= 7 ? 1 : 2, index), null);
        }

        var payload = await GetListAsync($"/holiday?year=2025&page={page}&pageSize=3", token);

        payload.Items.Count.ShouldBe(expectedItemCount);
        payload.TotalCount.ShouldBe(7);
        payload.TotalPages.ShouldBe(3);
        payload.HasNext.ShouldBe(expectedHasNext);
        payload.HasPrevious.ShouldBe(expectedHasPrevious);
    }

    private async Task<ResponseListHolidaysJson> GetListAsync(string uri, string token)
    {
        var response = await _client.GetAuthAsync(uri, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseListHolidaysJson>();
    }

    private static string QueryDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
