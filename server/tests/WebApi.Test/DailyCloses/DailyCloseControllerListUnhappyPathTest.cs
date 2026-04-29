using System.Globalization;
using System.Net;
using server.Application.UseCases.DailyCloses.List;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerListUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_ShouldReturn401_WhenNoToken()
    {
        var httpResponse = await _client.GetAsync("/dailyclose");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenPageIsZero()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcList400Page");

        var httpResponse = await _client.GetAuthAsync("/dailyclose?page=0", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_INVALID);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenPageSizeIsOverTheCap()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcList400PageSize");

        var httpResponse = await _client.GetAuthAsync(
            $"/dailyclose?pageSize={ListDailyClosesFluentValidation.MaximumPageSize + 1}",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(
            string.Format(
                ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_SIZE_INVALID,
                1,
                ListDailyClosesFluentValidation.MaximumPageSize));
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenPageSizeIsZero()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcList400PageSizeZero");

        var httpResponse = await _client.GetAuthAsync("/dailyclose?pageSize=0", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(
            string.Format(
                ResourcesErrorMessages.DAILYCLOSE_LIST_PAGE_SIZE_INVALID,
                1,
                ListDailyClosesFluentValidation.MaximumPageSize));
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenDateFromIsAfterDateTo()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcList400DateRange");
        var dateFrom = QueryDate(new DateTime(2025, 3, 11));
        var dateTo = QueryDate(new DateTime(2025, 3, 10));

        var httpResponse = await _client.GetAuthAsync(
            $"/dailyclose?dateFrom={dateFrom}&dateTo={dateTo}",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LIST_DATE_RANGE_INVALID);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenMineAndOperatorIdAreCombined()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcList400MineOperator");

        var httpResponse = await _client.GetAuthAsync($"/dailyclose?mine=true&operatorId={Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }

    private static string QueryDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
