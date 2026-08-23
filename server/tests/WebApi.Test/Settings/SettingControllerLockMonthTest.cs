using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using server.Application.UseCases.Settings.LockMonth;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Settings;

[Collection(ServerApiCollection.Name)]
public sealed class SettingControllerLockMonthTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task LockMonth_ShouldReturn401_WhenUnauthenticated()
    {
        var month = MonthsAgo(1);

        var response = await _client.PostAsJsonAsync(
            "/setting/lock-month",
            new RequestLockSettingMonthJson { Year = month.Year, Month = month.Month });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LockMonth_ShouldReturn403_WhenMember()
    {
        var ctx = await SeedContext("SettingLockMember", MonthsAgo(1), role: Role.Member);

        var response = await Lock(ctx, ctx.TargetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public async Task LockMonth_ShouldReturn200AndPersistMonthEnd_WhenEmptyMonthHasNoExpectedActivity()
    {
        var ctx = await SeedContext("SettingLockEmpty", MonthsAgo(1));

        var response = await Lock(ctx, ctx.TargetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var payload = await response.ReadContentAsync<ResponseSettingJson>();
        payload.LockDate.ShouldBe(MonthEnd(ctx.TargetMonth));
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(MonthEnd(ctx.TargetMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldReturnExactUnapprovedCloseKey()
    {
        var ctx = await SeedContext("SettingLockUnapproved", MonthsAgo(1));
        await factory.SeedDailyCloseAsync(
            ctx.BranchId,
            ctx.TerminalId,
            ctx.TargetMonth.AddDays(2),
            DailyCloseStatus.Submitted);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_UNAPPROVED_CLOSE);
    }

    [Fact]
    public async Task LockMonth_ShouldReturnExactDraftTransactionKey()
    {
        var ctx = await SeedContext("SettingLockDraft", MonthsAgo(1));
        await SeedTransaction(ctx, ctx.TerminalId, ctx.TargetMonth.AddDays(3), TransactionStatus.Draft);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_DRAFT_TRANSACTIONS);
    }

    [Fact]
    public async Task LockMonth_ShouldReturnExactNoCloseMonthKey_ForImportedTerminalActivity()
    {
        var ctx = await SeedContext("SettingLockNoClose", MonthsAgo(1));
        await SeedTransaction(ctx, ctx.TerminalId, ctx.TargetMonth.AddDays(4));

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_NO_CLOSE_WITH_EXPECTED_ACTIVITY);
    }

    [Fact]
    public async Task LockMonth_ShouldReturnExactMissingExpectedCloseKey_WhenAnotherApprovedCloseExists()
    {
        var ctx = await SeedContext("SettingLockMissing", MonthsAgo(1));
        await factory.SeedDailyCloseAsync(
            ctx.BranchId,
            ctx.TerminalId,
            ctx.TargetMonth.AddDays(1),
            DailyCloseStatus.Approved);
        var secondTerminal = await factory.SeedAccountAsync(ctx.BranchId, AccountType.Terminal);
        await SeedTransaction(ctx, secondTerminal.Id, ctx.TargetMonth.AddDays(5));

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_MISSING_EXPECTED_CLOSE);
    }

    [Fact]
    public async Task LockMonth_ShouldRejectJuneEquivalent_WhenPriorMonthIsUnresolved()
    {
        var requestedMonth = MonthsAgo(1);
        var priorMonth = requestedMonth.AddMonths(-1);
        var ctx = await SeedContext("SettingLockSkipped", requestedMonth, priorMonth);
        await factory.SeedDailyCloseAsync(
            ctx.BranchId,
            ctx.TerminalId,
            priorMonth.AddDays(2),
            DailyCloseStatus.Draft);

        await AssertConflict(ctx, IntermediateUnresolvedMessage(priorMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldUseCreatedAtMonthAsFreshBranchFloor()
    {
        var targetMonth = MonthsAgo(1);
        var ctx = await SeedContext("SettingLockFreshFloor", targetMonth, targetMonth);

        var response = await Lock(ctx, targetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(MonthEnd(targetMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldReturnExactBranchFloorKey_WhenRequestPredatesOperationalFloor()
    {
        var floorMonth = MonthsAgo(1);
        var requestedMonth = floorMonth.AddMonths(-1);
        var ctx = await SeedContext("SettingLockBeforeFloor", requestedMonth, floorMonth);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_BEFORE_BRANCH_FLOOR);
    }

    [Fact]
    public async Task LockMonth_ShouldRejectDraftTransactionBeforeCreatedAt_WithBlockingMonth()
    {
        var targetMonth = MonthsAgo(1);
        var earlierMonth = targetMonth.AddMonths(-1);
        var ctx = await SeedContext("SettingLockDataFloorDraft", targetMonth, targetMonth);
        var bank = await factory.SeedAccountAsync(ctx.BranchId, AccountType.BankAccount);
        await SeedTransaction(ctx, bank.Id, earlierMonth.AddDays(4), TransactionStatus.Draft);

        await AssertConflict(ctx, IntermediateUnresolvedMessage(earlierMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldRejectUnapprovedCloseBeforeCreatedAt_WithBlockingMonth()
    {
        var targetMonth = MonthsAgo(1);
        var earlierMonth = targetMonth.AddMonths(-1);
        var ctx = await SeedContext("SettingLockDataFloorClose", targetMonth, targetMonth);
        await factory.SeedDailyCloseAsync(
            ctx.BranchId,
            ctx.TerminalId,
            earlierMonth.AddDays(5),
            DailyCloseStatus.Draft);

        await AssertConflict(ctx, IntermediateUnresolvedMessage(earlierMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldSilentlyValidateCleanDataBeforeCreatedAt()
    {
        var targetMonth = MonthsAgo(1);
        var earlierMonth = targetMonth.AddMonths(-1);
        var ctx = await SeedContext("SettingLockDataFloorClean", targetMonth, targetMonth);
        var bank = await factory.SeedAccountAsync(ctx.BranchId, AccountType.BankAccount);
        await SeedTransaction(ctx, bank.Id, earlierMonth.AddDays(6));

        var response = await Lock(ctx, targetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(MonthEnd(targetMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldReturnExactAlreadyLockedKey_OnSameMonthReplay()
    {
        var ctx = await SeedContext("SettingLockReplay", MonthsAgo(1));
        (await Lock(ctx, ctx.TargetMonth)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertConflict(
            ctx,
            ResourcesErrorMessages.SETTING_LOCK_MONTH_ALREADY_LOCKED,
            MonthEnd(ctx.TargetMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldClampLegacyPartialBoundaryToOperationalFloor()
    {
        var targetMonth = MonthsAgo(1);
        var ctx = await SeedContext("SettingLockLegacyLow", targetMonth, targetMonth);
        await SetLockDate(ctx.SettingId, new DateTime(1, 1, 5));

        var response = await Lock(ctx, targetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(MonthEnd(targetMonth));
    }

    [Fact]
    public async Task LockMonth_ShouldRepairLegacyFutureBoundaryThroughValidatedCommand()
    {
        var targetMonth = MonthsAgo(1);
        var ctx = await SeedContext("SettingLockLegacyFuture", targetMonth, targetMonth);
        await SetLockDate(ctx.SettingId, new DateTime(2030, 12, 31));

        var response = await Lock(ctx, targetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate.ShouldBe(MonthEnd(targetMonth));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task LockMonth_ShouldReturnExactCurrentOrFutureKey(int monthOffset)
    {
        var requestedMonth = CurrentMonth().AddMonths(monthOffset);
        var ctx = await SeedContext("SettingLockCurrentFuture", requestedMonth, MonthsAgo(1));

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_CURRENT_OR_FUTURE);
    }

    [Fact]
    public async Task LockMonth_ShouldTreatWeekendHolidayAndPreActivationActivityExactlyLikeAnyOtherActivity()
    {
        var targetMonth = MonthsAgo(1);
        var ctx = await SeedContext("SettingLockCalendarNeutral", targetMonth);
        var saturday = Enumerable.Range(0, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month))
            .Select(offset => targetMonth.AddDays(offset))
            .First(date => date.DayOfWeek == DayOfWeek.Saturday);
        await factory.SeedHolidayAsync(ctx.BranchId, saturday, "Holiday on operating Saturday");
        // The Terminal row was created now, after this historical activity date. Activation windows
        // do not alter the rule: persistence-bypassed direct activity still makes the pair expected.
        await SeedTransaction(ctx, ctx.TerminalId, saturday);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_NO_CLOSE_WITH_EXPECTED_ACTIVITY);
    }

    [Fact]
    public async Task LockMonth_ShouldIgnorePairedTabFiado_WhenThereIsNoDirectTerminalActivity()
    {
        var targetMonth = MonthsAgo(1);
        var ctx = await SeedContext("SettingLockPairedTab", targetMonth);
        var tab = await factory.SeedAccountAsync(ctx.BranchId, AccountType.Tab);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
            var terminal = await dbContext.Accounts.FindAsync(ctx.TerminalId);
            terminal.ShouldNotBeNull();
            terminal.TabAccountId = tab.Id;
            await dbContext.SaveChangesAsync();
        }
        await SeedTransaction(ctx, tab.Id, targetMonth.AddDays(6));

        var response = await Lock(ctx, targetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LockMonth_ShouldReturn400WithExactValidationKeys()
    {
        var ctx = await SeedContext("SettingLockValidation", MonthsAgo(1));

        var yearResponse = await Lock(ctx, new DateTime(1999, 5, 1));
        yearResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await yearResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(string.Format(
                ResourcesErrorMessages.SETTING_LOCK_MONTH_YEAR_OUT_OF_RANGE,
                LockSettingMonthFluentValidation.MinimumYear,
                LockSettingMonthFluentValidation.MaximumYear));

        var monthResponse = await _client.PostAuthAsync(
            "/setting/lock-month",
            new RequestLockSettingMonthJson { Year = 2025, Month = 13 },
            ctx.Token,
            expectedVersion: 1);
        monthResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await monthResponse.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SETTING_LOCK_MONTH_INVALID);
    }

    private async Task AssertConflict(
        LockContext ctx,
        string expectedMessage,
        DateTime? expectedLockDate = null)
    {
        var response = await Lock(ctx, ctx.TargetMonth);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(expectedMessage);
        (await factory.ReloadAsync<Setting>(ctx.SettingId))!.LockDate
            .ShouldBe(expectedLockDate ?? DateTime.MinValue);
    }

    private async Task<HttpResponseMessage> Lock(LockContext ctx, DateTime month)
    {
        var setting = await factory.ReloadAsync<Setting>(ctx.SettingId);
        setting.ShouldNotBeNull();
        return await _client.PostAuthAsync(
            "/setting/lock-month",
            new RequestLockSettingMonthJson { Year = month.Year, Month = month.Month },
            ctx.Token,
            expectedVersion: setting.Version);
    }

    private async Task<LockContext> SeedContext(
        string label,
        DateTime targetMonth,
        DateTime? branchFloor = null,
        Role role = Role.Admin)
    {
        var createdAt = (branchFloor ?? targetMonth).AddDays(1).AddHours(12);
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            label,
            role,
            createdAt);
        var setting = await factory.SeedSettingAsync(branch.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);

        return new LockContext(
            user.Id,
            branch.Id,
            setting.Id,
            terminal.Id,
            op.Id,
            category.Id,
            transactionType.Id,
            token,
            targetMonth);
    }

    private Task<Transaction> SeedTransaction(
        LockContext ctx,
        Guid accountId,
        DateTime date,
        TransactionStatus status = TransactionStatus.Active)
    {
        return factory.SeedTransactionAsync(
            ctx.BranchId,
            accountId,
            ctx.TransactionTypeId,
            ctx.CategoryId,
            Direction.In,
            ctx.OperatorId,
            createdByUserId: ctx.UserId,
            date: date,
            status: status);
    }

    private async Task SetLockDate(Guid settingId, DateTime lockDate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
        var setting = await dbContext.Settings.FindAsync(settingId);
        setting.ShouldNotBeNull();
        setting.LockDate = lockDate;
        await dbContext.SaveChangesAsync();
    }

    private static DateTime CurrentMonth()
    {
        var today = new BranchClock().LocalBusinessDate(DateTime.UtcNow);
        return new DateTime(today.Year, today.Month, 1);
    }

    private static DateTime MonthsAgo(int count) => CurrentMonth().AddMonths(-count);
    private static DateTime MonthEnd(DateTime month) => month.AddMonths(1).AddDays(-1);
    private static string IntermediateUnresolvedMessage(DateTime month) => string.Format(
        ResourcesErrorMessages.SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED,
        month.ToString("MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));

    private sealed record LockContext(
        Guid UserId,
        Guid BranchId,
        Guid SettingId,
        Guid TerminalId,
        Guid OperatorId,
        Guid CategoryId,
        Guid TransactionTypeId,
        string Token,
        DateTime TargetMonth);
}
