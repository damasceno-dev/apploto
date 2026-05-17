using System.Net;
using server.Application.UseCases.TimeEntries.List;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

[Collection(ServerApiCollection.Name)]
public class TimeEntryControllerListUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/timeentry");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenPageIsLessThanOne()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListPageInvalid", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync("/timeentry?Page=0", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_LIST_PAGE_INVALID);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenPageSizeExceedsMaximum()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListPageSizeInvalid", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);

        var overMax = ListTimeEntriesFluentValidation.MaximumPageSize + 1;
        var httpResponse = await _client.GetAuthAsync($"/timeentry?PageSize={overMax}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.TIMEENTRY_LIST_PAGE_SIZE_INVALID,
            1,
            ListTimeEntriesFluentValidation.MaximumPageSize));
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenDateFromIsAfterDateTo()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListDateRangeInvalid", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync("/timeentry?DateFrom=2026-05-10&DateTo=2026-05-01", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_LIST_DATE_RANGE_INVALID);
    }

    [Fact]
    public async Task List_ShouldReturn400_WhenMineIsCombinedWithExplicitOperatorId()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListMineMutex", Role.Manager);
        await factory.SeedSettingAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync($"/timeentry?Mine=true&OperatorId={Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_LIST_MINE_AND_OPERATOR_ID_CONFLICT);
    }

    [Fact]
    public async Task List_ShouldReturn200WithEmptyPage_WhenMemberHasNoLinkedOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListMemberEmpty", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        // Seed an entry that the empty-scope short-circuit must hide.
        var otherOp = await factory.SeedOperatorAsync(branch.Id);
        var date = SpLocalDate().AddDays(-1);
        await TimeEntrySegmentTestHelpers.SeedTimeEntryWithSegmentsAsync(
            factory, branch.Id, otherOp.Id, date,
            segments: [(date.AddHours(8), date.AddHours(17), true)]);

        var httpResponse = await _client.GetAuthAsync("/timeentry", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListTimeEntriesJson>();
        payload.Items.ShouldBeEmpty();
        payload.TotalCount.ShouldBe(0);
        payload.TotalPages.ShouldBe(0);
        payload.HasNext.ShouldBeFalse();
        payload.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public async Task List_ShouldReturn403_WhenMemberAsksForAnotherOperatorsRows()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("TEListMemberForeignOp", Role.Member);
        await factory.SeedSettingAsync(branch.Id);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var otherOp = await factory.SeedOperatorAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync($"/timeentry?OperatorId={otherOp.Id}", token);

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
