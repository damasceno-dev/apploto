using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerUpsertHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upsert_MemberTapE2E_ShouldOpenCloseOpenCloseWithStableSegments()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMemberTapE2E", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var date = SpLocalDateNow();

        var firstOpen = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Open), token);
        firstOpen.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstOpenPayload = await firstOpen.ReadContentAsync<ResponseTimeEntryJson>();
        firstOpenPayload.IsInProgress.ShouldBeTrue();
        firstOpenPayload.Segments.Count.ShouldBe(1);
        firstOpenPayload.Segments.Single().ClockOut.ShouldBeNull();
        var firstSegmentId = firstOpenPayload.Segments.Single().Id;

        var firstClose = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Close), token);
        firstClose.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstClosePayload = await firstClose.ReadContentAsync<ResponseTimeEntryJson>();
        firstClosePayload.Id.ShouldBe(firstOpenPayload.Id);
        firstClosePayload.IsInProgress.ShouldBeFalse();
        firstClosePayload.Segments.Single().Id.ShouldBe(firstSegmentId);
        firstClosePayload.Segments.Single().ClockOut.ShouldNotBeNull();

        var secondOpen = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Open), token);
        secondOpen.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondOpenPayload = await secondOpen.ReadContentAsync<ResponseTimeEntryJson>();
        secondOpenPayload.Id.ShouldBe(firstOpenPayload.Id);
        secondOpenPayload.IsInProgress.ShouldBeTrue();
        secondOpenPayload.Segments.Count.ShouldBe(2);
        var secondSegmentId = secondOpenPayload.Segments.Last().Id;
        secondOpenPayload.Segments.Last().ClockOut.ShouldBeNull();

        var secondClose = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Close), token);
        secondClose.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondClosePayload = await secondClose.ReadContentAsync<ResponseTimeEntryJson>();
        secondClosePayload.Id.ShouldBe(firstOpenPayload.Id);
        secondClosePayload.IsInProgress.ShouldBeFalse();
        secondClosePayload.Segments.Count.ShouldBe(2);
        secondClosePayload.Segments[0].Id.ShouldBe(firstSegmentId);
        secondClosePayload.Segments[1].Id.ShouldBe(secondSegmentId);
        secondClosePayload.Segments.All(segment => segment.ClockOut is not null).ShouldBeTrue();

        var persisted = await ReloadTimeEntryWithSegmentsAsync(firstOpenPayload.Id);
        persisted.Segments.Count(segment => segment.Active).ShouldBe(2);
        persisted.Segments.Where(segment => segment.Active).All(segment => segment.ClockOut is not null).ShouldBeTrue();
    }

    [Fact]
    public async Task Upsert_MemberRetryIdempotency_ShouldNotCreatePhantomSegments()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertMemberRetry", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var date = SpLocalDateNow();

        var firstOpen = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Open), token);
        firstOpen.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstOpenPayload = await firstOpen.ReadContentAsync<ResponseTimeEntryJson>();

        var openRetry = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Open), token);
        openRetry.StatusCode.ShouldBe(HttpStatusCode.OK);
        var openRetryPayload = await openRetry.ReadContentAsync<ResponseTimeEntryJson>();
        openRetryPayload.Id.ShouldBe(firstOpenPayload.Id);
        openRetryPayload.Segments.Count.ShouldBe(1);
        openRetryPayload.Segments.Single().Id.ShouldBe(firstOpenPayload.Segments.Single().Id);

        var firstClose = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Close), token);
        firstClose.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstClosePayload = await firstClose.ReadContentAsync<ResponseTimeEntryJson>();

        var closeRetry = await _client.PutAuthAsync("/timeentry", MemberTap(op.Id, date, TimeEntryTapAction.Close), token);
        closeRetry.StatusCode.ShouldBe(HttpStatusCode.OK);
        var closeRetryPayload = await closeRetry.ReadContentAsync<ResponseTimeEntryJson>();
        closeRetryPayload.Id.ShouldBe(firstOpenPayload.Id);
        closeRetryPayload.IsInProgress.ShouldBeFalse();
        closeRetryPayload.Segments.Count.ShouldBe(1);
        closeRetryPayload.Segments.Single().Id.ShouldBe(firstClosePayload.Segments.Single().Id);
        closeRetryPayload.Segments.Single().ClockOut.ShouldBe(firstClosePayload.Segments.Single().ClockOut);

        var persisted = await ReloadTimeEntryWithSegmentsAsync(firstOpenPayload.Id);
        persisted.Segments.Count(segment => segment.Active).ShouldBe(1);
        persisted.Segments.Single(segment => segment.Active).ClockOut.ShouldNotBeNull();
    }

    [Fact]
    public async Task Upsert_AdminExplicitTimesBackfill_ShouldReturn200AndPersistSegment()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminBackfill", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = new DateTime(2026, 5, 8);
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(8))
            .WithClockOut(date.AddHours(17))
            .Build();
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(segment);

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.OperatorId.ShouldBe(op.Id);
        payload.Status.ShouldBe(TimeEntryStatus.Present);
        payload.IsInProgress.ShouldBeFalse();
        payload.Segments.Count.ShouldBe(1);
        payload.Segments.Single().ClockIn.ShouldBe(date.AddHours(8));
        payload.Segments.Single().ClockOut.ShouldBe(date.AddHours(17));
        payload.TotalHours.ShouldBe(8m, tolerance: 0.01m);

        var persisted = await ReloadTimeEntryWithSegmentsAsync(payload.Id);
        persisted.Segments.Single(segmentArg => segmentArg.Active).ClockOut.ShouldBe(date.AddHours(17));
    }

    [Fact]
    public async Task Upsert_AdminMultiSegmentUpsert_ShouldApplyGapAwareTotals()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminMulti", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = new DateTime(2026, 5, 8);
        var morning = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(8))
            .WithClockOut(date.AddHours(12))
            .Build();
        var afternoon = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(13))
            .WithClockOut(date.AddHours(17))
            .Build();
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(morning, afternoon);

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Segments.Count.ShouldBe(2);
        payload.IsInProgress.ShouldBeFalse();
        payload.TotalHours.ShouldBe(8m, tolerance: 0.01m);

        var persisted = await ReloadTimeEntryWithSegmentsAsync(payload.Id);
        persisted.Segments.Count(segment => segment.Active).ShouldBe(2);
    }

    [Fact]
    public async Task Upsert_AdminSingleSegmentOvernightBackfill_ShouldPersistNextDayClockOut()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEUpsertAdminOvernight", Role.Admin);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = new DateTime(2026, 5, 8);
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(new DateTime(2026, 5, 8, 22, 0, 0))
            .WithClockOut(new DateTime(2026, 5, 9, 6, 0, 0))
            .Build();
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(segment);

        var httpResponse = await _client.PutAuthAsync("/timeentry", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTimeEntryJson>();
        payload.Segments.Single().ClockOut.ShouldBe(new DateTime(2026, 5, 9, 6, 0, 0));
        payload.TotalHours.ShouldBe(7m, tolerance: 0.01m);

        var persisted = await ReloadTimeEntryWithSegmentsAsync(payload.Id);
        persisted.Segments.Single(segmentArg => segmentArg.Active).ClockOut!.Value.Date.ShouldBe(new DateTime(2026, 5, 9));
    }

    private async Task<TimeEntry> ReloadTimeEntryWithSegmentsAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        return await dbContext.TimeEntries
            .AsNoTracking()
            .Include(entry => entry.Segments)
            .SingleAsync(entry => entry.Id == id);
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
