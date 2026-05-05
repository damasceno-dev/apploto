using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Application.UseCases.Holidays.Update;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerUpdateUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var httpResponse = await _client.PutAsync($"/holiday/{Guid.NewGuid()}", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ShouldReturn403_WhenMemberAttempts()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolUpdate403", Role.Member);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 9, 7), "Holiday");
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/holiday/{holiday.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenHolidayDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolUpdate404", Role.Manager);
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/holiday/{Guid.NewGuid()}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenHolidayBelongsToAnotherBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("HolUpdate404BranchA", Role.Manager);
        var branchB = await factory.SeedBranchForOtherContextAsync("HolUpdate404BranchB");
        var holidayInBranchB = await factory.SeedHolidayAsync(branchB.Id, new DateTime(2025, 9, 7), "Other branch");
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/holiday/{holidayInBranchB.Id}", request, tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenDescriptionExceedsMaxLength()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolUpdate400Desc", Role.Manager);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 9, 7), "Holiday");
        var request = new RequestUpdateHolidayJsonBuilder()
            .WithDescription(new string('A', UpdateHolidayFluentValidation.MaximumDescriptionLength + 1))
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/holiday/{holiday.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.HOLIDAY_DESCRIPTION_MAX_LENGTH,
            UpdateHolidayFluentValidation.MaximumDescriptionLength));
    }
}
