using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerCreateHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldReturn201AndPersistHolidays_WhenManagerSubmitsValidBatch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolCreateMgr", Role.Manager);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithHolidays([
                new RequestCreateHolidayJson { Date = new DateTime(2025, 9, 7), Description = "Independência do Brasil" },
                new RequestCreateHolidayJson { Date = new DateTime(2025, 11, 2), Description = "Finados" }
            ])
            .Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateHolidaysJson>();
        payload.Items.Count.ShouldBe(2);
        payload.Items.All(h => h.BranchId == branch.Id).ShouldBeTrue();
        payload.Items.All(h => h.Active).ShouldBeTrue();
        payload.Items.Select(h => h.Date).ShouldContain(new DateTime(2025, 9, 7));
        payload.Items.Select(h => h.Date).ShouldContain(new DateTime(2025, 11, 2));

        foreach (var item in payload.Items)
        {
            var persisted = await factory.ReloadAsync<Holiday>(item.Id);
            persisted.ShouldNotBeNull();
            persisted.BranchId.ShouldBe(branch.Id);
            persisted.Active.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenAdminSubmitsValidBatch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolCreateAdmin", Role.Admin);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(new DateTime(2025, 10, 12), "Nossa Senhora Aparecida")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateHolidaysJson>();
        payload.Items.Count.ShouldBe(1);
        payload.Items[0].BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Holiday>(payload.Items[0].Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task Create_ShouldPersistNullDescription_WhenDescriptionIsOmitted()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolCreateNoDesc", Role.Manager);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(new DateTime(2025, 11, 15), null)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/holiday", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateHolidaysJson>();
        payload.Items[0].Description.ShouldBeNull();

        var persisted = await factory.ReloadAsync<Holiday>(payload.Items[0].Id);
        persisted.ShouldNotBeNull();
        persisted.Description.ShouldBeNull();
    }
}
