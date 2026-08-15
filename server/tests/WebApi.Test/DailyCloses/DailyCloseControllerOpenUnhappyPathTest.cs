using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerOpenUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Open_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestOpenDailyCloseJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/dailyclose", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Open_ShouldReturn400_WhenDateIsDefault()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpen400Date", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(default)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_DATE_REQUIRED);
    }

    [Fact]
    public async Task Open_ShouldReturn400_WhenAccountIdIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpen400Account", Role.Manager);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithAccountId(Guid.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_REQUIRED);
    }

    [Fact]
    public async Task Open_ShouldReturn403_WhenMemberHasNoLinkedOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenMember403NoOp", Role.Member);
        // No operator seeded for this user — member has no linked operator.
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday())
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task Open_ShouldReturn403_WhenMemberTargetsUnlinkedAccount()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenMember403", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        // Operator has no OperatorAccount link to this account.

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday())
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task Open_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenXBranch", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DcOpenXBranchOther");
        var crossBranchAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(DateTime.Today)
            .WithAccountId(crossBranchAccount.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task Open_ShouldReturn409_WhenLockDateBlocksTargetDate()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenLockDate", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var targetDate = new DateTime(2025, 1, 10);
        await factory.SeedSettingAsync(branch.Id, lockDate: targetDate);

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(targetDate)
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/dailyclose", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
    }

    [Fact]
    public async Task Open_ShouldReturn409WithDateConflictKey_WhenConcurrentOpensRace()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcOpenRace", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var raceDate = new DateTime(2025, 6, 15);

        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var firstRequest = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(raceDate)
            .WithAccountId(account.Id)
            .Build();
        var secondRequest = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(raceDate)
            .WithAccountId(account.Id)
            .Build();

        var responses = await Task.WhenAll(
            firstClient.PostAuthAsync("/dailyclose", firstRequest, token),
            secondClient.PostAuthAsync("/dailyclose", secondRequest, token));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);

        var conflictResponse = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var payload = await conflictResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_DATE_CONFLICT);
    }

    [Theory]
    [InlineData(AccountType.Tab)]
    [InlineData(AccountType.BankAccount)]
    public async Task Open_ShouldReturn409_WhenAccountIsNotTerminal(AccountType accountType)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcOpenType{accountType}",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, accountType);
        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday())
            .WithAccountId(account.Id)
            .Build();

        var response = await _client.PostAuthAsync("/dailyclose", request, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_NOT_TERMINAL);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Open_ShouldReturn409_WhenMemberUsesNonCurrentDate(int dayOffset)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcOpenMemberDate{dayOffset}",
            Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday().AddDays(dayOffset))
            .WithAccountId(account.Id)
            .Build();

        var response = await _client.PostAuthAsync("/dailyclose", request, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(dayOffset > 0
            ? ResourcesErrorMessages.DAILYCLOSE_FUTURE_DATE_NOT_ALLOWED
            : ResourcesErrorMessages.DAILYCLOSE_MEMBER_OPEN_REQUIRES_TODAY);
    }

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public async Task Open_ShouldReturn409_WhenElevatedRoleUsesFutureDate(Role role)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcOpenFuture{role}",
            role);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(LocalToday().AddDays(1))
            .WithAccountId(account.Id)
            .Build();

        var response = await _client.PostAuthAsync("/dailyclose", request, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_FUTURE_DATE_NOT_ALLOWED);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
