using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerDeactivateUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deactivate_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.DeleteAsync($"/holiday/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenMemberAttempts()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolDeactivate403", Role.Member);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 9, 7), "Holiday");

        var httpResponse = await _client.DeleteAuthAsync($"/holiday/{holiday.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenHolidayDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolDeactivate404", Role.Manager);

        var httpResponse = await _client.DeleteAuthAsync($"/holiday/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenHolidayBelongsToAnotherBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("HolDeactivate404BranchA", Role.Manager);
        var branchB = await factory.SeedBranchForOtherContextAsync("HolDeactivate404BranchB");
        var holidayInBranchB = await factory.SeedHolidayAsync(branchB.Id, new DateTime(2025, 9, 7), "Other branch");

        var httpResponse = await _client.DeleteAuthAsync($"/holiday/{holidayInBranchB.Id}", tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Phase 6 catch-up: exercise the 400 path declared on DELETE /holiday/{id}.
    // The route constraint {holidayId:guid} accepts Guid.Empty as a syntactically
    // valid GUID, so the use case's HOLIDAY_ID_EMPTY validation is reachable.
    [Fact]
    public async Task Deactivate_ShouldReturn400_WhenHolidayIdIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolDeactivate400Empty", Role.Manager);

        var httpResponse = await _client.DeleteAuthAsync($"/holiday/{Guid.Empty}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_ID_EMPTY);
    }
}
