using System.Net;
using server.Application.UseCases.Reports;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerDailyLedgerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DailyLedger_ShouldReturn401_WhenTokenIsMissing()
    {
        var url = $"/report/daily-ledger?accountId={Guid.NewGuid()}&dateFrom=2025-01-01&dateTo=2025-01-31";

        var httpResponse = await _client.GetAsync(url);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — Member role cannot access daily ledger
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DailyLedger_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DLUnhappy403", Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id);
        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-01-01&dateTo=2025-01-31";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // 404 — account belongs to a different branch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DailyLedger_ShouldReturn404_WhenAccountBelongsToAnotherBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DLUnhappy404a");
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DLUnhappy404b-other");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id);

        var url = $"/report/daily-ledger?accountId={otherAccount.Id}&dateFrom=2025-01-01&dateTo=2025-01-31";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    // -------------------------------------------------------------------------
    // 400 — validation failures
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DailyLedger_ShouldReturn400_WhenDateRangeIsInverted()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DLUnhappy400a");
        var account = await factory.SeedAccountAsync(branch.Id);
        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-02-01&dateTo=2025-01-01";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    [Fact]
    public async Task DailyLedger_ShouldReturn400_WhenDateRangeExceedsMaxDays()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DLUnhappy400b");
        var account = await factory.SeedAccountAsync(branch.Id);
        var dateFrom = new DateTime(2025, 1, 1);
        var dateTo = dateFrom.AddDays(ReportValidationExtensions.DateRangeMaxDays + 1);
        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom={dateFrom:yyyy-MM-dd}&dateTo={dateTo:yyyy-MM-dd}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_TOO_WIDE);
    }

    [Fact]
    public async Task DailyLedger_ShouldReturn400_WhenAccountIdIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DLUnhappy400c");
        var url = $"/report/daily-ledger?accountId={Guid.Empty}&dateFrom=2025-01-01&dateTo=2025-01-31";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_ACCOUNT_ID_REQUIRED);
    }

    [Fact]
    public async Task DailyLedger_ShouldReturn400_WhenPageSizeExceedsMax()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DLUnhappy400d");
        var account = await factory.SeedAccountAsync(branch.Id);
        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-01-01&dateTo=2025-01-31&pageSize={ReportValidationExtensions.PageSizeMax + 1}";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }
}
