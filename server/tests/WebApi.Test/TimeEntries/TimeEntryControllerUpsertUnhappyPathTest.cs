using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerUpsertUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upsert_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = MemberTap(Guid.NewGuid(), SpLocalDateNow(), TimeEntryTapAction.Open);

        var httpResponse = await _client.PutAsync("/timeentry", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Upsert_MemberWithSegments_ShouldReturn400()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMemberSegments", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var date = SpLocalDateNow();
        var request = new
        {
            OperatorId = op.Id,
            Date = date,
            Status = TimeEntryStatus.Present,
            Segments = new[]
            {
                new
                {
                    ClockIn = date.AddHours(8),
                    ClockOut = (DateTime?)date.AddHours(17)
                }
            }
        };

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MEMBER_SHOULD_NOT_SEND_SEGMENTS);
    }

    [Fact]
    public async Task Upsert_MemberWithNeitherActionNorSegments_ShouldReturn400()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMemberNeither", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var request = new
        {
            OperatorId = op.Id,
            Date = SpLocalDateNow(),
            Status = TimeEntryStatus.Present
        };

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MEMBER_TAP_ACTION_REQUIRED);
    }

    [Fact]
    public async Task Upsert_AdminWithSegmentsNull_ShouldReturn400()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminSegmentsNull", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new
        {
            OperatorId = op.Id,
            Date = new DateTime(2026, 5, 8),
            Status = TimeEntryStatus.Present
        };

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_ADMIN_REQUIRES_SEGMENTS);
    }

    [Fact]
    public async Task Upsert_AdminWithAction_ShouldReturn400()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminAction", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var request = new
        {
            OperatorId = op.Id,
            Date = new DateTime(2026, 5, 8),
            Status = TimeEntryStatus.Present,
            Action = TimeEntryTapAction.Open
        };

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_ADMIN_SHOULD_NOT_SEND_TAP_ACTION);
    }

    [Fact]
    public async Task Upsert_AdminClockInEditAttempt_ShouldReturn400()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminClockInLocked", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = new DateTime(2026, 5, 8);
        var create = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(new RequestTimeEntrySegmentJsonBuilder()
                .WithClockIn(date.AddHours(8))
                .WithClockOut(date.AddHours(12))
                .Build());
        var createHttp = await _client.PutAuthAsync("/timeentry", create, token);
        var created = await createHttp.ReadContentAsync<server.Communication.Responses.ResponseTimeEntryJson>();
        var segmentId = created.Segments.Single().Id;

        var edit = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(new RequestTimeEntrySegmentJsonBuilder()
                .WithId(segmentId)
                .WithClockIn(date.AddHours(9))
                .WithClockOut(date.AddHours(12))
                .Build());

        var httpResponse = await _client.PutAuthAsync("/timeentry", edit, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_LOCKED);
    }

    [Fact]
    public async Task Upsert_AdminStatusChangeToVacationWithActiveSegments_ShouldReturn409()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminStatusCleanup", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = new DateTime(2026, 5, 8);
        var create = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(new RequestTimeEntrySegmentJsonBuilder()
                .WithClockIn(date.AddHours(8))
                .WithClockOut(date.AddHours(12))
                .Build());
        var createHttp = await _client.PutAuthAsync("/timeentry", create, token);
        createHttp.StatusCode.ShouldBe(HttpStatusCode.OK);

        var vacation = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Vacation)
            .BuildAdminSnapshot();

        var httpResponse = await _client.PutAuthAsync("/timeentry", vacation, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_STATUS_CHANGE_REQUIRES_SEGMENT_CLEANUP);
    }

    [Fact]
    public async Task Upsert_AdminOutOfDayBoundsSegment_ShouldReturn400()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminOutOfBounds", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = new DateTime(2026, 5, 8);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(new RequestTimeEntrySegmentJsonBuilder()
                .WithClockIn(date.AddDays(1))
                .WithClockOut(date.AddDays(1).AddHours(2))
                .Build());

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS);
    }

    [Fact]
    public async Task Upsert_AdminClockOutBeforeClockIn_ShouldReturn400()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminClockOutBefore", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = new DateTime(2026, 5, 8);
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(new RequestTimeEntrySegmentJsonBuilder()
                .WithClockIn(date.AddHours(12))
                .WithClockOut(date.AddHours(11))
                .Build());

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
    }

    [Fact]
    public async Task Upsert_ConcurrentMemberOpenRace_ShouldReturnOpenSegmentConflict()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMemberOpenRace", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var date = SpLocalDateNow();
        await SeedClosedTimeEntryAsync(branch.Id, op.Id, date);

        await InstallOpenSegmentDelayTriggerAsync();
        try
        {
            using var firstClient = factory.CreateClient();
            using var secondClient = factory.CreateClient();
            var request = MemberTap(op.Id, date, TimeEntryTapAction.Open);

            var responses = await Task.WhenAll(
                firstClient.PutAuthAsync("/timeentry", request, token),
                secondClient.PutAuthAsync("/timeentry", request, token));

            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            var payload = await conflict.ReadContentAsync<TestResponseErrorJson>();
            payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_OPEN_SEGMENT_CONFLICT);
        }
        finally
        {
            await DropOpenSegmentDelayTriggerAsync();
        }
    }

    private async Task SeedClosedTimeEntryAsync(Guid branchId, Guid operatorId, DateTime date)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var entry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Date = date,
            Status = TimeEntryStatus.Present,
            TotalHours = 4m,
            BalanceHours = -3.33m,
            OperatorId = operatorId,
            BranchId = branchId
        };
        dbContext.TimeEntries.Add(entry);
        dbContext.TimeEntrySegments.Add(new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            TimeEntryId = entry.Id,
            ClockIn = date,
            ClockOut = date.AddMinutes(1)
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task InstallOpenSegmentDelayTriggerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE OR REPLACE FUNCTION test_sleep_open_timeentry_segment()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW."ClockOut" IS NULL THEN
                    PERFORM pg_sleep(0.5);
                END IF;
                RETURN NEW;
            END;
            $$;

            DROP TRIGGER IF EXISTS test_sleep_open_timeentry_segment_trigger ON "TimeEntrySegments";

            CREATE TRIGGER test_sleep_open_timeentry_segment_trigger
            BEFORE INSERT ON "TimeEntrySegments"
            FOR EACH ROW
            EXECUTE FUNCTION test_sleep_open_timeentry_segment();
            """);
    }

    private async Task DropOpenSegmentDelayTriggerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DROP TRIGGER IF EXISTS test_sleep_open_timeentry_segment_trigger ON "TimeEntrySegments";
            DROP FUNCTION IF EXISTS test_sleep_open_timeentry_segment();
            """);
    }

    private static object MemberTap(Guid operatorId, DateTime date, TimeEntryTapAction action)
    {
        return new
        {
            OperatorId = operatorId,
            Date = date,
            Status = TimeEntryStatus.Present,
            Action = action
        };
    }

    private static DateTime SpLocalDateNow()
    {
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date, DateTimeKind.Unspecified);
    }
}
