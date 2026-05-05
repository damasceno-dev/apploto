using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerUpdateHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_ShouldReturn200AndPersistNewDescription_WhenManagerSubmitsValidRequest()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolUpdateMgr", Role.Manager);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 9, 7), "Original");
        var request = new RequestUpdateHolidayJsonBuilder().WithDescription("Updated").Build();

        var httpResponse = await _client.PutAuthAsync($"/holiday/{holiday.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseHolidayJson>();
        payload.Description.ShouldBe("Updated");
        payload.Id.ShouldBe(holiday.Id);
        payload.Date.ShouldBe(new DateTime(2025, 9, 7));

        var persisted = await factory.ReloadAsync<Holiday>(holiday.Id);
        persisted.ShouldNotBeNull();
        persisted.Description.ShouldBe("Updated");
    }

    [Fact]
    public async Task Update_ShouldReturn200AndSetDescriptionNull_WhenDescriptionIsOmitted()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolUpdateNull", Role.Admin);
        var holiday = await factory.SeedHolidayAsync(branch.Id, new DateTime(2025, 10, 12), "Remove me");
        var request = new RequestUpdateHolidayJsonBuilder().WithDescription(null).Build();

        var httpResponse = await _client.PutAuthAsync($"/holiday/{holiday.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseHolidayJson>();
        payload.Description.ShouldBeNull();

        var persisted = await factory.ReloadAsync<Holiday>(holiday.Id);
        persisted.ShouldNotBeNull();
        persisted.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ShouldNotMutateDate_WhenDescriptionIsUpdated()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolUpdateDate", Role.Manager);
        var originalDate = new DateTime(2025, 11, 15);
        var holiday = await factory.SeedHolidayAsync(branch.Id, originalDate, "Proclamação");
        var request = new RequestUpdateHolidayJsonBuilder().WithDescription("Proclamação da República").Build();

        var httpResponse = await _client.PutAuthAsync($"/holiday/{holiday.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseHolidayJson>();
        payload.Date.ShouldBe(originalDate);

        var persisted = await factory.ReloadAsync<Holiday>(holiday.Id);
        persisted.ShouldNotBeNull();
        persisted.Date.ShouldBe(originalDate);
    }
}
