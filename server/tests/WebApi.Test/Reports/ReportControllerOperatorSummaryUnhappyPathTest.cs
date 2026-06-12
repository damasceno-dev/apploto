using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerOperatorSummaryUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn401_WhenTokenIsMissing()
    {
        const string url = "/report/operator-summary?mine=true&dateFrom=2025-01-01&dateTo=2025-01-31";

        var httpResponse = await _client.GetAsync(url);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 400 — inverted date range
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn400_WhenDateFromIsAfterDateTo()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OSUnhappy400Inverted", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var url = $"/report/operator-summary?operatorId={op.Id}&dateFrom=2025-02-01&dateTo=2025-01-01";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    // -------------------------------------------------------------------------
    // 400 — Mine = true combined with an explicit OperatorId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn400_WhenMineIsCombinedWithOperatorId()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OSUnhappy400Mutex", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var url = $"/report/operator-summary?mine=true&operatorId={op.Id}&dateFrom=2025-01-01&dateTo=2025-01-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_MINE_AND_OPERATOR_ID_CONFLICT);
    }

    // -------------------------------------------------------------------------
    // 400 — Manager/Admin must supply Mine or OperatorId (Members fall back
    // to their own linked operator instead)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn400_WhenManagerOmitsMineAndOperatorId()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OSUnhappy400Missing", Role.Manager);

        const string url = "/report/operator-summary?dateFrom=2025-01-01&dateTo=2025-01-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_OPERATOR_ID_REQUIRED);
    }

    // -------------------------------------------------------------------------
    // 403 — linked Member targeting another operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn403_WhenMemberTargetsAnotherOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OSUnhappy403", Role.Member);
        var linkedOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(linkedOperator.Id, account.Id, isPrimary: true);
        var otherOperator = await factory.SeedOperatorAsync(branch.Id);

        var url = $"/report/operator-summary?operatorId={otherOperator.Id}&dateFrom=2025-01-01&dateTo=2025-01-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_MEMBER_NOT_OWN_OPERATOR);
    }

    // -------------------------------------------------------------------------
    // 404 — Manager targeting a cross-branch operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn404_WhenOperatorBelongsToAnotherBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OSUnhappy404Cross", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("OSUnhappy404Cross-other");
        var crossBranchOperator = await factory.SeedOperatorAsync(otherBranch.Id);

        var url = $"/report/operator-summary?operatorId={crossBranchOperator.Id}&dateFrom=2025-01-01&dateTo=2025-01-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    // -------------------------------------------------------------------------
    // 404 — Manager using Mine = true without an own linked operator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OperatorSummary_ShouldReturn404_WhenManagerUsesMineWithoutLinkedOperator()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("OSUnhappy404Mine", Role.Manager);

        const string url = "/report/operator-summary?mine=true&dateFrom=2025-01-01&dateTo=2025-01-31";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }
}
