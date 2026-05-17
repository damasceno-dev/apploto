using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerGetUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync($"/timeentry/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenIdDoesNotExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetMissing", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenEntryBelongsToAnotherBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("TEGetOtherBranchA", Role.Manager);
        await factory.SeedSettingAsync(branchA.Id);
        var (_, branchB, _, _) = await factory.SeedFullBranchContextAsync("TEGetOtherBranchB", Role.Manager);
        await factory.SeedSettingAsync(branchB.Id);
        var opB = await factory.SeedOperatorAsync(branchB.Id);
        var dateB = SpLocalDate().AddDays(-1);
        var entryB = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branchB.Id, opB.Id, dateB,
            segments: [(dateB.AddHours(8), dateB.AddHours(17), true)]);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{entryB.Id}", tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenMemberHasNoLinkedOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetMemberNoLink", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-1);
        var entry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, op.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{entry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenMemberTargetsAnotherOperatorsEntry()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEGetMemberOtherOp", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);

        var otherOp = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-1);
        var otherEntry = await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, otherOp.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var httpResponse = await _client.GetAuthAsync($"/timeentry/{otherEntry.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
    }

    private static DateTime SpLocalDate()
    {
        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date, DateTimeKind.Unspecified);
    }
}
