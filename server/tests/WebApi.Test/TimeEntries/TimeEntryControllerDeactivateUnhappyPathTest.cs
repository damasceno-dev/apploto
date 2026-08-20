using System.Net;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerDeactivateUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deactivate_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.DeleteAsync($"/timeentry/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenMemberAttempts()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEDeactivate403", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);

        // Seed a TimeEntry directly via the DbContext — the Member token can't insert
        // anything beyond same-day-Present, and this scenario is about the role check
        // running before the load, not the load itself.
        var seededTimeEntry = await SeedTimeEntryDirectAsync(factory, branch.Id, op.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/{seededTimeEntry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenTimeEntryDoesNotExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEDeactivate404Missing", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenTimeEntryBelongsToAnotherBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("TEDeactivate404BranchA", Role.Manager);
        await factory.SeedSettingAsync(branchA.Id);

        var branchB = await factory.SeedBranchForOtherContextAsync("TEDeactivate404BranchB");
        await factory.SeedSettingAsync(branchB.Id);
        var opInBranchB = await factory.SeedOperatorAsync(branchB.Id);
        var timeEntryInBranchB = await SeedTimeEntryDirectAsync(factory, branchB.Id, opInBranchB.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/{timeEntryInBranchB.Id}", tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenTimeEntryIsAlreadyInactive()
    {
        // Repo's GetByIdAndBranchId filters Active = true; double-deactivate surfaces
        // as 404 — same convention as M2 Operator deactivate.
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEDeactivate404Inactive", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var date = TodayUnspecified();
        var segment = new CommonTestUtilities.Requests.RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(date.AddHours(8))
            .WithClockOut(date.AddHours(17))
            .Build();
        var insertRequest = new CommonTestUtilities.Requests.RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(op.Id)
            .WithDate(date)
            .WithStatus(TimeEntryStatus.Present)
            .BuildAdminSnapshot(segment);
        var insertHttp = await _client.PutAuthAsync("/timeentry", insertRequest, token);
        var inserted = await insertHttp.ReadContentAsync<ResponseTimeEntryJson>();

        var firstDelete = await _client.DeleteAuthAsync($"/timeentry/{inserted.Id}", token);
        firstDelete.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondDelete = await _client.DeleteAuthAsync($"/timeentry/{inserted.Id}", token);
        secondDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await secondDelete.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn409_WhenTimeEntryDateIsLocked()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEDeactivateLocked", Role.Admin);
        var date = new DateTime(2026, 5, 20);
        await factory.SeedSettingAsync(branch.Id, lockDate: new DateTime(2026, 5, 31));
        var op = await factory.SeedOperatorAsync(branch.Id);
        var timeEntry = await SeedTimeEntryDirectAsync(factory, branch.Id, op.Id, date);

        var httpResponse = await _client.DeleteAuthAsync($"/timeentry/{timeEntry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
        (await factory.ReloadAsync<server.Domain.Entities.TimeEntry>(timeEntry.Id))!.Active.ShouldBeTrue();
    }

    private static async Task<server.Domain.Entities.TimeEntry> SeedTimeEntryDirectAsync(
        ServerWebApplicationFactory factory,
        Guid branchId,
        Guid operatorId,
        DateTime? date = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();

        var timeEntry = new server.Domain.Entities.TimeEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Date = date ?? TodayUnspecified(),
            Status = TimeEntryStatus.Present,
            TotalHours = 8m,
            BalanceHours = 0.67m,
            OperatorId = operatorId,
            BranchId = branchId
        };
        dbContext.TimeEntries.Add(timeEntry);
        dbContext.TimeEntrySegments.Add(new server.Domain.Entities.TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            TimeEntryId = timeEntry.Id,
            ClockIn = timeEntry.Date.AddHours(8),
            ClockOut = timeEntry.Date.AddHours(17)
        });
        await dbContext.SaveChangesAsync();
        return timeEntry;
    }

    private static DateTime TodayUnspecified()
    {
        return DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
    }
}
