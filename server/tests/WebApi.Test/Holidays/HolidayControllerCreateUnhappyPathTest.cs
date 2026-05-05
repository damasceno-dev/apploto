using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Application.UseCases.Holidays.Create;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerCreateUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateHolidaysJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/holiday", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Create_ShouldReturn403_WhenMemberAttempts()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolCreate403", Role.Member);
        var request = new RequestCreateHolidaysJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenBatchIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolCreate400Empty", Role.Manager);
        var request = new RequestCreateHolidaysJsonBuilder().WithEmptyList().Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_CREATE_BATCH_EMPTY);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenRowDateIsDefault()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolCreate400Date", Role.Manager);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(default)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_DATE_REQUIRED);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenDescriptionExceedsMaxLength()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolCreate400Desc", Role.Manager);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(new DateTime(2025, 9, 7), new string('A', CreateHolidaysFluentValidation.MaximumDescriptionLength + 1))
            .Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.HOLIDAY_DESCRIPTION_MAX_LENGTH, CreateHolidaysFluentValidation.MaximumDescriptionLength));
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenDuplicateDatesAreInSameRequest()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolCreate400Dup", Role.Manager);
        var duplicateDate = new DateTime(2025, 9, 7);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithHolidays([
                new RequestCreateHolidayJson { Date = duplicateDate, Description = "First" },
                new RequestCreateHolidayJson { Date = duplicateDate, Description = "Second" }
            ])
            .Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenDateAlreadyExistsInBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolCreate409Existing", Role.Manager);
        var existingDate = new DateTime(2025, 12, 25);
        await factory.SeedHolidayAsync(branch.Id, existingDate, "Natal");

        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(existingDate, "Natal duplicate")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
    }

    [Fact]
    public async Task Create_ShouldReturn409WithDateConflictKey_WhenConcurrentBatchesRace()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolCreateRace", Role.Manager);
        var raceDate = new DateTime(2025, 8, 25);

        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var firstRequest = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(raceDate, "Race 1")
            .Build();
        var secondRequest = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(raceDate, "Race 2")
            .Build();

        var responses = await Task.WhenAll(
            firstClient.PostAuthAsync("/holiday", firstRequest, token),
            secondClient.PostAuthAsync("/holiday", secondRequest, token));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);

        var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var payload = await conflictResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
    }
}
