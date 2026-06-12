using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerCashVarianceUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturn401_WhenTokenIsMissing()
    {
        const string url = "/report/cash-variance?dateFrom=2025-01-01&dateTo=2025-01-31&page=1&pageSize=50";

        var httpResponse = await _client.GetAsync(url);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — Member role cannot access cash variance summary
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CVUnhappy403", Role.Member);
        const string url = "/report/cash-variance?dateFrom=2025-01-01&dateTo=2025-01-31&page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // 400 — inverted dates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturn400_WhenDateFromIsAfterDateTo()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVUnhappy400Inverted");
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        const string url = "/report/cash-variance?dateFrom=2025-02-01&dateTo=2025-01-01&page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    // -------------------------------------------------------------------------
    // 400 — missing dateFrom
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturn400_WhenDateFromIsMissing()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVUnhappy400DateFrom");
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        const string url = "/report/cash-variance?dateTo=2025-01-31&page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }

    // -------------------------------------------------------------------------
    // 400 — page zero
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturn400_WhenPageIsZero()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVUnhappy400Page");
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        const string url = "/report/cash-variance?dateFrom=2025-01-01&dateTo=2025-01-31&page=0&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_PAGE_INVALID);
    }

    // -------------------------------------------------------------------------
    // 404 — cross-branch accountId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturn404_WhenAccountIdBelongsToAnotherBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVUnhappy404", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        var otherBranch = await factory.SeedBranchForOtherContextAsync("CVUnhappy404-other");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);

        var url = $"/report/cash-variance?dateFrom=2025-01-01&dateTo=2025-01-31&accountId={otherAccount.Id}&page=1&pageSize=50";

        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }
}
