using System.Net;
using server.Application.UseCases.Holidays.List;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerListUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/holiday");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenPageIsZero()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolList400Page", Role.Manager);

        var httpResponse = await _client.GetAuthAsync("/holiday?page=0", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_LIST_PAGE_INVALID);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenPageSizeExceedsMaximum()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolList400PageSize", Role.Manager);
        var overMax = ListHolidaysFluentValidation.MaximumPageSize + 1;

        var httpResponse = await _client.GetAuthAsync($"/holiday?pageSize={overMax}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.HOLIDAY_LIST_PAGE_SIZE_INVALID, 1, ListHolidaysFluentValidation.MaximumPageSize));
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenDateFromIsAfterDateTo()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolList400DateRange", Role.Manager);

        var httpResponse = await _client.GetAuthAsync("/holiday?dateFrom=2025-12-31&dateTo=2025-01-01", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_LIST_DATE_RANGE_INVALID);
    }
}
