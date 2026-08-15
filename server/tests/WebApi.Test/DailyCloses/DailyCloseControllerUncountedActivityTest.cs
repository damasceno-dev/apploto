using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerUncountedActivityTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Submit_ShouldReportEarliestGapAndAllowEscapeByCountingEachBlockingDay()
    {
        var context = await SeedContextAsync("DcGapEarliestEscape");
        var earliest = context.CloseDate.AddDays(-4);
        var later = context.CloseDate.AddDays(-2);
        await SeedTransactionAsync(context, earliest, TransactionStatus.Active);
        await SeedTransactionAsync(context, later, TransactionStatus.Active);

        var first = await _client.PostAuthAsync($"/dailyclose/{context.CloseId}/submit", context.Token);
        first.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var firstError = await first.ReadContentAsync<TestResponseErrorJson>();
        firstError.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.DAILYCLOSE_PRIOR_DAY_NOT_COUNTED,
            earliest.ToString("dd/MM/yyyy")));

        await factory.SeedDailyCloseAsync(context.BranchId, context.AccountId, earliest);
        var second = await _client.PostAuthAsync($"/dailyclose/{context.CloseId}/submit", context.Token);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var secondError = await second.ReadContentAsync<TestResponseErrorJson>();
        secondError.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.DAILYCLOSE_PRIOR_DAY_NOT_COUNTED,
            later.ToString("dd/MM/yyyy")));

        await factory.SeedDailyCloseAsync(context.BranchId, context.AccountId, later);
        var retry = await _client.PostAuthAsync($"/dailyclose/{context.CloseId}/submit", context.Token);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_ShouldIgnoreDraftCancelledAndSoftDeletedTransactions()
    {
        var context = await SeedContextAsync("DcGapIgnoredRows");
        await SeedTransactionAsync(context, context.CloseDate.AddDays(-4), TransactionStatus.Draft);
        await SeedTransactionAsync(context, context.CloseDate.AddDays(-3), TransactionStatus.Cancelled);
        await SeedTransactionAsync(
            context,
            context.CloseDate.AddDays(-2),
            TransactionStatus.Active,
            active: false);

        var response = await _client.PostAuthAsync($"/dailyclose/{context.CloseId}/submit", context.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_ShouldAcceptCountedDraftAsOpeningSourceEligibility()
    {
        var context = await SeedContextAsync("DcGapCountedDraft");
        var activityDate = context.CloseDate.AddDays(-2);
        await SeedTransactionAsync(context, activityDate, TransactionStatus.Active);
        await factory.SeedDailyCloseAsync(
            context.BranchId,
            context.AccountId,
            activityDate,
            DailyCloseStatus.Draft,
            itemsRecorded: true);

        var response = await _client.PostAuthAsync($"/dailyclose/{context.CloseId}/submit", context.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_ShouldUseExclusiveOpeningLockAndCurrentDateBoundaries()
    {
        var context = await SeedContextAsync("DcGapExclusiveBounds");
        var lockDate = context.CloseDate.AddDays(-4);
        var openingDate = context.CloseDate.AddDays(-2);
        await factory.SeedSettingAsync(context.BranchId, lockDate);
        await factory.SeedDailyCloseAsync(
            context.BranchId,
            context.AccountId,
            openingDate,
            DailyCloseStatus.Draft,
            itemsRecorded: true);
        await SeedTransactionAsync(context, lockDate, TransactionStatus.Active);
        await SeedTransactionAsync(context, openingDate, TransactionStatus.Active);
        await SeedTransactionAsync(context, context.CloseDate, TransactionStatus.Active);

        var response = await _client.PostAuthAsync($"/dailyclose/{context.CloseId}/submit", context.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<GapContext> SeedContextAsync(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var closeDate = LocalToday();
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, closeDate);
        return new GapContext(
            token,
            user.Id,
            branch.Id,
            account.Id,
            op.Id,
            category.Id,
            transactionType.Id,
            close.Id,
            closeDate);
    }

    private Task SeedTransactionAsync(
        GapContext context,
        DateTime date,
        TransactionStatus status,
        bool active = true)
    {
        return factory.SeedTransactionAsync(
            context.BranchId,
            context.AccountId,
            context.TransactionTypeId,
            context.CategoryId,
            Direction.In,
            context.OperatorId,
            context.UserId,
            date,
            status: status,
            active: active);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    private sealed record GapContext(
        string Token,
        Guid UserId,
        Guid BranchId,
        Guid AccountId,
        Guid OperatorId,
        Guid CategoryId,
        Guid TransactionTypeId,
        Guid CloseId,
        DateTime CloseDate);
}
