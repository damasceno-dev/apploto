using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerDeactivateHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deactivate_ShouldReturn204AndSoftDelete_WhenManagerSubmitsValidId()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolDeactivateMgr", Role.Manager);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 9, 7), "Independência");

        var httpResponse = await _client.DeleteAuthAsync($"/holiday/{holiday.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var persisted = await factory.ReloadAsync<Holiday>(holiday.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldReturn204_WhenAdminSubmitsValidId()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolDeactivateAdmin", Role.Admin);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 10, 12), "Nossa Senhora Aparecida");

        var httpResponse = await _client.DeleteAuthAsync($"/holiday/{holiday.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var persisted = await factory.ReloadAsync<Holiday>(holiday.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ThenCreateSameDate_ShouldReturn201_ProvingFilteredUniqueIndexAllowsRecreation()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolDeactivateRecreate", Role.Manager);
        var recreationDate = new DateTime(2025, 12, 25);
        var holiday = await factory.SeedHolidayAsync(branch.Id, recreationDate, "Natal original");

        var deleteResponse = await _client.DeleteAuthAsync($"/holiday/{holiday.Id}", token);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deactivatedRecord = await factory.ReloadAsync<Holiday>(holiday.Id);
        deactivatedRecord.ShouldNotBeNull();
        deactivatedRecord.Active.ShouldBeFalse();

        var createRequest = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(recreationDate, "Natal recriado")
            .Build();
        var createResponse = await _client.PostAuthAsync("/holiday", createRequest, token);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var newHoliday = await factory.ReloadAsync<Holiday>(
            (await createResponse.ReadContentAsync<server.Communication.Responses.ResponseCreateHolidaysJson>())
            .Items[0].Id);
        newHoliday.ShouldNotBeNull();
        newHoliday.Active.ShouldBeTrue();
        newHoliday.Date.ShouldBe(recreationDate);
        newHoliday.Description.ShouldBe("Natal recriado");
    }
}
