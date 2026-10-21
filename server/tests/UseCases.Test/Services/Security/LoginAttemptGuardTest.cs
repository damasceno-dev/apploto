using CommonTestUtilities.Services;
using server.Application.Services.Security;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Security;

public sealed class LoginAttemptGuardTest
{
    private const string Email = "person@example.invalid";
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly LoginProtectionOptions Options = LoginProtectionOptionsFactory.Create(null, "3", "5", "10", "20");

    [Fact]
    public async Task TheLimitThFailure_ShouldBlockForTheCooldown_AndTheExactEndShouldAdmit()
    {
        var (guard, clock, _) = Create();
        await Fail(guard, Email, 2);
        clock.Advance(TimeSpan.FromMinutes(9));
        await Fail(guard, Email, 1); // third failure inside the 10-minute window

        clock.Advance(TimeSpan.FromMinutes(20) - TimeSpan.FromMilliseconds(1));
        var refused = await Should.ThrowAsync<TooManyAttemptsException>(() => guard.Build().Admit(Email));
        refused.Message.ShouldBe(ResourcesErrorMessages.SECURITY_RATE_LIMITED);
        refused.RetryAfter.ShouldBe(TimeSpan.FromMilliseconds(1));

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await guard.Build().Admit(Email);
    }

    [Fact]
    public async Task AFailureAtTheWindowEnd_ShouldOpenANewWindow()
    {
        var (guard, clock, throttles) = Create();
        await Fail(guard, Email, 2);
        clock.Advance(TimeSpan.FromMinutes(10));

        await Fail(guard, Email, 1);

        throttles.Rows.ShouldAllBe(r => r.FailureCount == 1 && r.WindowStartedAt == clock.GetUtcNow().UtcDateTime &&
                                        r.BlockedUntil == null && r.ExpiresAt == clock.GetUtcNow().UtcDateTime.AddMinutes(10));
        await guard.Build().Admit(Email);
    }

    [Fact]
    public async Task AFailureAfterABlock_ShouldStartAFreshWindow_NotExtendTheBlock()
    {
        var (guard, clock, throttles) = Create();
        await Fail(guard, Email, 3);
        var client = throttles.Rows.Single(r => r.KeyHash != LoginAttemptGuard.AccountKey(Email));
        client.ExpiresAt.ShouldBe(Start.UtcDateTime.AddMinutes(20));

        clock.Advance(TimeSpan.FromMinutes(20));
        await Fail(guard, Email, 1);

        client.FailureCount.ShouldBe(1);
        client.BlockedUntil.ShouldBeNull();
        throttles.Rows.Single(r => r.KeyHash == LoginAttemptGuard.AccountKey(Email)).FailureCount.ShouldBe(1);
    }

    [Fact]
    public async Task ABlockedNetwork_ShouldNotBlockAnother_UntilTheAccountBudgetIsSpent()
    {
        var (guard, _, _) = Create();
        await Fail(guard.FromNetwork("v4:198.51.100.1"), Email, 3);
        await Should.ThrowAsync<TooManyAttemptsException>(() => guard.Build().Admit(Email));

        await Fail(guard.FromNetwork("v4:198.51.100.2"), Email, 2); // five failures across networks = account limit

        var refused = await Should.ThrowAsync<TooManyAttemptsException>(() => guard.FromNetwork("v4:198.51.100.3").Build().Admit(Email));
        refused.RetryAfter.ShouldBe(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task BudgetsBelongToOneAccount_AndHoldOnlyHashes()
    {
        var (guard, _, throttles) = Create();
        await Fail(guard, Email, 3);

        await guard.Build().Admit("other@example.invalid");
        throttles.Rows.Count.ShouldBe(2);
        throttles.Rows.ShouldAllBe(r => r.KeyHash.Length == 64 && r.AccountKeyHash == LoginAttemptGuard.AccountKey(Email));
        throttles.Rows.Select(r => r.KeyHash).ShouldNotContain(k => k.Contains("example") || k.Contains("203.0.113"));
    }

    [Fact]
    public async Task Success_ShouldRemoveOnlyTheNetworkRow()
    {
        var (guard, _, throttles) = Create();
        await Fail(guard, Email, 2);

        var guardInstance = guard.Build();
        guardInstance.Succeeded(await guardInstance.Admit(Email));

        throttles.Rows.ShouldHaveSingleItem().KeyHash.ShouldBe(LoginAttemptGuard.AccountKey(Email));
    }

    [Fact]
    public async Task TheLongerOfTwoBlocks_ShouldBeTheRetryAfter()
    {
        var (guard, clock, _) = Create();
        await Fail(guard.FromNetwork("v4:198.51.100.1"), Email, 3); // network blocked until +20 min
        clock.Advance(TimeSpan.FromMinutes(5));
        await Fail(guard.FromNetwork("v4:198.51.100.2"), Email, 2); // account blocked until +25 min

        var refused = await Should.ThrowAsync<TooManyAttemptsException>(() => guard.FromNetwork("v4:198.51.100.1").Build().Admit(Email));
        refused.RetryAfter.ShouldBe(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task KeyDerivation_IsAPersistedFormat_AndMustNotDrift()
    {
        // Changing either derivation would silently orphan every stored budget, so the exact bytes are pinned.
        var (guard, _, throttles) = Create();
        await Fail(guard.FromNetwork(LoginAttemptGuardBuilder.DefaultNetwork), Email, 1);

        LoginAttemptGuard.AccountKey(Email).ShouldBe("A9424EF322465F644629D56403DCF96B870A461B3801F707D1F81603F464D4A4");
        throttles.Rows.Select(r => r.KeyHash).Order(StringComparer.Ordinal).ShouldBe([
            "22921647E6BCEE3EE77586413991B0594DB06E677A5CE2F3D0D2FE102D79849F",
            "A9424EF322465F644629D56403DCF96B870A461B3801F707D1F81603F464D4A4"]);
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(10, 5)]
    [InlineData(7, 7)]
    public async Task RetryAfter_ShouldBeTheLongerRemainingBlock_WhicheverRowHoldsIt(int accountMinutes, int clientMinutes)
    {
        var (guard, _, throttles) = Create();
        await Fail(guard, Email, 1); // creates both rows for the default network
        var now = Start.UtcDateTime;
        foreach (var row in throttles.Rows)
            row.BlockedUntil = now.AddMinutes(row.KeyHash == LoginAttemptGuard.AccountKey(Email) ? accountMinutes : clientMinutes);

        var refused = await Should.ThrowAsync<TooManyAttemptsException>(() => guard.Build().Admit(Email));
        refused.RetryAfter.ShouldBe(TimeSpan.FromMinutes(Math.Max(accountMinutes, clientMinutes)));
    }

    [Fact]
    public async Task OneBlockedRow_ShouldBeEnough_WhicheverItIs()
    {
        var (guard, _, throttles) = Create();
        await Fail(guard, Email, 1);
        var client = throttles.Rows.Single(r => r.KeyHash != LoginAttemptGuard.AccountKey(Email));
        client.BlockedUntil = Start.UtcDateTime.AddMinutes(3);

        (await Should.ThrowAsync<TooManyAttemptsException>(() => guard.Build().Admit(Email))).RetryAfter.ShouldBe(TimeSpan.FromMinutes(3));
        client.BlockedUntil = null;
        throttles.Rows.Single(r => r.KeyHash == LoginAttemptGuard.AccountKey(Email)).BlockedUntil = Start.UtcDateTime.AddMinutes(4);
        (await Should.ThrowAsync<TooManyAttemptsException>(() => guard.Build().Admit(Email))).RetryAfter.ShouldBe(TimeSpan.FromMinutes(4));
    }

    private static (LoginAttemptGuardBuilder Guard, SettableUtcTimeProvider Clock, CommonTestUtilities.Repositories.LoginThrottleRepositoryBuilder Throttles) Create()
    {
        var clock = new SettableUtcTimeProvider(Start);
        var guard = new LoginAttemptGuardBuilder().WithTime(clock).WithOptions(Options);
        return (guard, clock, guard.Throttles);
    }

    private static async Task Fail(LoginAttemptGuardBuilder builder, string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            var guard = builder.Build();
            await guard.Failed(await guard.Admit(email));
        }
    }
}
