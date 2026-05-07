using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerUpsertHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upsert_ShouldReturn200AndPersist_WhenManagerInsertsPresentEntry()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMgrInsert", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.OperatorId.ShouldBe(op.Id);
        payload.OperatorName.ShouldBe(op.Name);
        payload.BranchId.ShouldBe(branch.Id);
        payload.Status.ShouldBe(TimeEntryStatus.Present);
        payload.ClockIn.ShouldBe(new TimeOnly(8, 0));
        payload.ClockOut.ShouldBe(new TimeOnly(17, 0));
        payload.TotalHours.ShouldBe(8m, tolerance: 0.01m);
        payload.Active.ShouldBeTrue();
        payload.UpdatedByUserId.ShouldNotBeNull();

        var persisted = await factory.ReloadAsync<TimeEntry>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.OperatorId.ShouldBe(op.Id);
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.Status.ShouldBe(TimeEntryStatus.Present);
        persisted.TotalHours.ShouldBe(payload.TotalHours);
        persisted.BalanceHours.ShouldBe(payload.BalanceHours);
        persisted.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Upsert_ShouldReturn200AndUpdateInPlacePreservingId_WhenEntryAlreadyExists()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMgrUpdate", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = DateTime.UtcNow.Date;

        var insertRequest = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(12, 0))
            .Build();
        var firstHttp = await _client.PutAuthAsync("/timeentry", insertRequest, token);
        firstHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await firstHttp.ReadContentAsync<ResponseTimeEntryJson>();

        var updateRequest = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();
        var secondHttp = await _client.PutAuthAsync("/timeentry", updateRequest, token);

        secondHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPayload = await secondHttp.ReadContentAsync<ResponseTimeEntryJson>();
        secondPayload.Id.ShouldBe(firstPayload.Id);
        secondPayload.ClockOut.ShouldBe(new TimeOnly(17, 0));

        var persisted = await factory.ReloadAsync<TimeEntry>(firstPayload.Id);
        persisted.ShouldNotBeNull();
        persisted.Id.ShouldBe(firstPayload.Id);
        persisted.ClockOut.ShouldBe(new TimeOnly(17, 0));
        persisted.TotalHours.ShouldBe(secondPayload.TotalHours);
    }

    [Fact]
    public async Task Upsert_ShouldReturn200_WhenAdminInsertsAbonadoStatusWithoutClocks()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminAbo", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Vacation)
            .WithNoClocks()
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Status.ShouldBe(TimeEntryStatus.Vacation);
        payload.ClockIn.ShouldBeNull();
        payload.ClockOut.ShouldBeNull();
        payload.TotalHours.ShouldBe(7.33m);
        payload.BalanceHours.ShouldBe(0m);
    }

    [Fact]
    public async Task Upsert_ShouldReturn200_WhenMemberRecordsOwnSameDayPresent()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMemberSelf", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);

        // Use the São Paulo local date so IsSameLocalDay always passes during the test.
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var spLocalDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date;

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(spLocalDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.OperatorId.ShouldBe(op.Id);
    }

    [Fact]
    public async Task Upsert_ShouldReturn200WithIsInProgressTrue_WhenManagerSendsClockInOnly()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMgrPartial", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        // Use the São Paulo local date so the live-running same-day branch fires.
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var spLocalDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date;

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(spLocalDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(null)
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.IsInProgress.ShouldBeTrue();
        payload.ClockIn.ShouldBe(new TimeOnly(8, 0));
        payload.ClockOut.ShouldBeNull();

        var persisted = await factory.ReloadAsync<TimeEntry>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.ClockIn.ShouldBe(new TimeOnly(8, 0));
        persisted.ClockOut.ShouldBeNull();
        persisted.Status.ShouldBe(TimeEntryStatus.Present);
    }

    [Fact]
    public async Task Upsert_ShouldComputeFinalHoursAndIsInProgressFalse_WhenSecondTapCompletesShift()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertTwoTap", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var spLocalDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date;

        // First tap: ClockIn only (partial Present, live-running).
        var firstRequest = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(spLocalDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(null)
            .Build();
        var firstHttp = await _client.PutAuthAsync("/timeentry", firstRequest, token);
        firstHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await firstHttp.ReadContentAsync<ResponseTimeEntryJson>();
        firstPayload.IsInProgress.ShouldBeTrue();

        // Second tap: both clocks (final Present, standard Calculate).
        var secondRequest = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(spLocalDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();
        var secondHttp = await _client.PutAuthAsync("/timeentry", secondRequest, token);

        secondHttp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPayload = await secondHttp.ReadContentAsync<ResponseTimeEntryJson>();
        secondPayload.Id.ShouldBe(firstPayload.Id);
        secondPayload.IsInProgress.ShouldBeFalse();
        secondPayload.ClockOut.ShouldBe(new TimeOnly(17, 0));
        secondPayload.TotalHours.ShouldBe(8m, tolerance: 0.01m); // 9h gross − 1h lunch = 8h

        var persisted = await factory.ReloadAsync<TimeEntry>(firstPayload.Id);
        persisted.ShouldNotBeNull();
        persisted.Id.ShouldBe(firstPayload.Id);
        persisted.ClockOut.ShouldBe(new TimeOnly(17, 0));
        persisted.TotalHours.ShouldBe(secondPayload.TotalHours);
    }
}
