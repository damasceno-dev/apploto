using System.Net;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerDeactivateHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deactivate_ShouldReturn200AndSoftDeleteWithAuditStamps_WhenManagerSubmitsValidId()
    {
        var (_, branch, membership, token) = await factory.SeedFullBranchContextAsync("TEDeactivateMgr", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        // Insert a TimeEntry through the upsert endpoint so persistence and the row's
        // initial audit state come from the production code path.
        var insertRequest = new CommonTestUtilities.Requests.RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();
        var insertHttp = await _client.PutAuthAsync("/timeentry", insertRequest, token);
        insertHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var inserted = await insertHttp.ReadContentAsync<ResponseTimeEntryJson>();

        var deleteHttp = await _client.DeleteAuthAsync($"/timeentry/{inserted.Id}", token);

        deleteHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await deleteHttp.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Id.ShouldBe(inserted.Id);
        payload.Active.ShouldBeFalse();
        payload.OperatorId.ShouldBe(op.Id);
        payload.OperatorName.ShouldBe(op.Name);
        payload.UpdatedByUserId.ShouldBe(membership.UserId);
        payload.UpdatedAt.ShouldNotBeNull();

        var persisted = await factory.ReloadAsync<TimeEntry>(inserted.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
        persisted.UpdatedAt.ShouldNotBeNull();
        persisted.UpdatedByUserId.ShouldBe(membership.UserId);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn200_WhenAdminSubmitsValidId()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEDeactivateAdmin", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var insertRequest = new CommonTestUtilities.Requests.RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Vacation)
            .WithNoClocks()
            .Build();
        var insertHttp = await _client.PutAuthAsync("/timeentry", insertRequest, token);
        var inserted = await insertHttp.ReadContentAsync<ResponseTimeEntryJson>();

        var deleteHttp = await _client.DeleteAuthAsync($"/timeentry/{inserted.Id}", token);

        deleteHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persisted = await factory.ReloadAsync<TimeEntry>(inserted.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ThenReUpsertSameKey_ShouldReturn200_ProvingFilteredUniqueIndexAllowsRecreation()
    {
        // Soft-delete leaves the row in place; the filtered unique index
        // (BranchId, OperatorId, Date) WHERE Active = true must allow a new active row
        // for the same key.
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEDeactivateRecreate", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = DateTime.UtcNow.Date;

        var insertRequest = new CommonTestUtilities.Requests.RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();
        var firstInsert = await _client.PutAuthAsync("/timeentry", insertRequest, token);
        firstInsert.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await firstInsert.ReadContentAsync<ResponseTimeEntryJson>();

        var deleteHttp = await _client.DeleteAuthAsync($"/timeentry/{firstPayload.Id}", token);
        deleteHttp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deactivated = await factory.ReloadAsync<TimeEntry>(firstPayload.Id);
        deactivated.ShouldNotBeNull();
        deactivated.Active.ShouldBeFalse();

        var recreate = await _client.PutAuthAsync("/timeentry", insertRequest, token);
        recreate.StatusCode.ShouldBe(HttpStatusCode.OK);

        var recreatedPayload = await recreate.ReadContentAsync<ResponseTimeEntryJson>();
        recreatedPayload.Id.ShouldNotBe(firstPayload.Id);
        recreatedPayload.Active.ShouldBeTrue();

        var recreated = await factory.ReloadAsync<TimeEntry>(recreatedPayload.Id);
        recreated.ShouldNotBeNull();
        recreated.Active.ShouldBeTrue();
    }
}
