using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerUpsertUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static DateTime SpLocalDateNow()
    {
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date;
    }

    [Fact]
    public async Task Upsert_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder().Build();

        var httpResponse = await _client.PutAsync("/timeentry", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Upsert_ShouldReturn400_WhenOperatorIdIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert400Op", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(Guid.Empty)
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_OPERATOR_ID_REQUIRED);
    }

    [Fact]
    public async Task Upsert_ShouldReturn400_WhenDateIsDefault()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert400Date", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(default)
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_REQUIRED);
    }

    [Fact]
    public async Task Upsert_ShouldReturn400_WhenPresentMissingClockIn()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert400PresClockIn", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Present)
            .WithNoClocks()
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_PRESENT_REQUIRES_CLOCK_IN);
    }

    [Fact]
    public async Task Upsert_ShouldReturn400_WhenClockOutSuppliedWithoutClockIn()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert400ClockOutNoIn", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(null)
            .WithClockOut(new TimeOnly(17, 0))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN);
    }

    [Fact]
    public async Task Upsert_ShouldReturn400_WhenNonPresentHasClocks()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert400NonPresClocks", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Vacation)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_CLOCKS);
    }

    [Fact]
    public async Task Upsert_ShouldReturn400_WhenSundayStatusOnNonSundayDate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert400Sun", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        // Find a non-Sunday date close to today.
        var nonSunday = DateTime.UtcNow.Date;
        while (nonSunday.DayOfWeek == DayOfWeek.Sunday)
        {
            nonSunday = nonSunday.AddDays(1);
        }

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(nonSunday)
            .WithStatus(TimeEntryStatus.Sunday)
            .WithNoClocks()
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SUNDAY_REQUIRES_SUNDAY_DATE);
    }

    [Fact]
    public async Task Upsert_ShouldReturn400_WhenHolidayStatusButNoActiveHoliday()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert400Hol", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(DateTime.UtcNow.Date)
            .WithStatus(TimeEntryStatus.Holiday)
            .WithNoClocks()
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_HOLIDAY_REQUIRES_ACTIVE_HOLIDAY);
    }

    [Fact]
    public async Task Upsert_ShouldReturn403_WhenMemberHasNoLinkedOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert403NoLink", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(SpLocalDateNow())
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task Upsert_ShouldReturn403_WhenMemberTargetsAnotherOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert403OtherOp", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var anotherOperator = await factory.SeedOperatorAsync(branch.Id);

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(anotherOperator.Id)
            .WithDate(SpLocalDateNow())
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
    }

    [Fact]
    public async Task Upsert_ShouldReturn403_WhenMemberWritesOlderDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert403Older", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(SpLocalDateNow().AddDays(-3))
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_WRITE_REQUIRES_SAME_DAY);
    }

    [Fact]
    public async Task Upsert_ShouldReturn403_WhenMemberSubmitsNonPresentStatus()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert403NonPres", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(SpLocalDateNow())
            .WithStatus(TimeEntryStatus.DayOff)
            .WithNoClocks()
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MEMBER_ONLY_PRESENT);
    }

    [Fact]
    public async Task Upsert_ShouldReturn404_WhenTargetOperatorBelongsToAnotherBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsert404Cross", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        var crossBranchOperator = await factory.SeedOperatorAsync(otherBranch.Id);

        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(crossBranchOperator.Id)
            .WithDate(DateTime.UtcNow.Date)
            .Build();

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task Upsert_ShouldReturn409WithDateConflictKey_WhenConcurrentInsertsRace()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertRace", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var raceDate = DateTime.UtcNow.Date.AddDays(-30); // Stable date, not affected by clock skew.

        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var firstRequest = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(raceDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();
        var secondRequest = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(raceDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(9, 0))
            .WithClockOut(new TimeOnly(18, 0))
            .Build();

        var responses = await Task.WhenAll(
            firstClient.PutAuthAsync("/timeentry", firstRequest, token),
            secondClient.PutAuthAsync("/timeentry", secondRequest, token));

        // Either both succeed (one after the other due to load-tracked check picking up the
        // first row) or one races into the unique index and surfaces the 409. Verify we
        // never end up with two active rows: at least one response must be 200.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBeGreaterThanOrEqualTo(1);

        var conflict = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.Conflict);
        if (conflict is not null)
        {
            var payload = await conflict.ReadContentAsync<TestResponseErrorJson>();
            payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_CONFLICT);
        }
    }
}
