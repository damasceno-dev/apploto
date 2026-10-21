using server.Application.Services.Security;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.AccountSecurity;

public sealed class LoginProtectionOptionsFactoryTest
{
    [Fact]
    public void Defaults_ShouldBeTheRecordedPolicy()
    {
        var options = LoginProtectionOptionsFactory.Create(null, null, null, null, null);

        options.RequestsPerMinute.ShouldBe(LoginProtectionOptionsFactory.DefaultRequestsPerMinute);
        options.ClientFailureLimit.ShouldBe(LoginProtectionOptionsFactory.DefaultClientFailureLimit);
        options.AccountFailureLimit.ShouldBe(LoginProtectionOptionsFactory.DefaultAccountFailureLimit);
        options.FailureWindow.ShouldBe(TimeSpan.FromMinutes(LoginProtectionOptionsFactory.DefaultFailureWindowMinutes));
        options.Cooldown.ShouldBe(TimeSpan.FromMinutes(LoginProtectionOptionsFactory.DefaultCooldownMinutes));
    }

    [Fact]
    public void ExplicitValues_ShouldBeUsedAtTheirBounds()
    {
        var options = LoginProtectionOptionsFactory.Create(
            LoginProtectionOptionsFactory.MaxRequestsPerMinute.ToString(), "1",
            LoginProtectionOptionsFactory.MaxAccountFailureLimit.ToString(), "1",
            LoginProtectionOptionsFactory.MaxMinutes.ToString());

        options.RequestsPerMinute.ShouldBe(LoginProtectionOptionsFactory.MaxRequestsPerMinute);
        options.ClientFailureLimit.ShouldBe(1);
        options.AccountFailureLimit.ShouldBe(LoginProtectionOptionsFactory.MaxAccountFailureLimit);
        options.FailureWindow.ShouldBe(TimeSpan.FromMinutes(1));
        options.Cooldown.ShouldBe(TimeSpan.FromMinutes(LoginProtectionOptionsFactory.MaxMinutes));
    }

    [Fact]
    public void AccountBudget_MayEqualButNeverUndercutTheClientBudget()
    {
        LoginProtectionOptionsFactory.Create(null, "7", "7", null, null).AccountFailureLimit.ShouldBe(7);
        Should.Throw<InvalidOperationException>(() => LoginProtectionOptionsFactory.Create(null, "7", "6", null, null))
            .Message.ShouldContain("LoginAccountFailureLimit");
    }

    [Fact]
    public void ANetworkBudgetAboveTheDefaultAccountBudget_ShouldRequireAnExplicitAccountBudget()
    {
        var aboveDefault = (LoginProtectionOptionsFactory.DefaultAccountFailureLimit + 1).ToString();

        Should.Throw<InvalidOperationException>(() => LoginProtectionOptionsFactory.Create(null, aboveDefault, null, null, null))
            .Message.ShouldContain("AccountSecurity:LoginAccountFailureLimit");
        LoginProtectionOptionsFactory.Create(null, aboveDefault, aboveDefault, null, null).AccountFailureLimit
            .ShouldBe(LoginProtectionOptionsFactory.DefaultAccountFailureLimit + 1);
        LoginProtectionOptionsFactory.Create(null, LoginProtectionOptionsFactory.DefaultAccountFailureLimit.ToString(), null, null, null)
            .AccountFailureLimit.ShouldBe(LoginProtectionOptionsFactory.DefaultAccountFailureLimit);
    }

    [Theory]
    [InlineData(0, "LoginRequestsPerMinute")]
    [InlineData(1, "LoginClientFailureLimit")]
    [InlineData(2, "LoginAccountFailureLimit")]
    [InlineData(3, "LoginFailureWindowMinutes")]
    [InlineData(4, "LoginCooldownMinutes")]
    public void AnInvalidValue_ShouldNameItsSetting(int position, string key)
    {
        var values = new string?[5];
        values[position] = "0";
        Should.Throw<InvalidOperationException>(() => LoginProtectionOptionsFactory.Create(values[0], values[1], values[2], values[3], values[4]))
            .Message.ShouldContain("AccountSecurity:" + key);
    }

    [Theory]
    [InlineData("0", null, null, null, null)]
    [InlineData("1001", null, null, null, null)]
    [InlineData(null, "0", null, null, null)]
    [InlineData(null, "101", null, null, null)]
    [InlineData(null, null, "1001", null, null)]
    [InlineData(null, null, null, "0", null)]
    [InlineData(null, null, null, "1441", null)]
    [InlineData(null, null, null, null, "0")]
    [InlineData(null, null, null, null, "1441")]
    [InlineData("ten", null, null, null, null)]
    [InlineData(null, null, null, " ", null)]
    public void InvalidValues_ShouldStopStartup(string? requests, string? client, string? account, string? window, string? cooldown) =>
        Should.Throw<InvalidOperationException>(() => LoginProtectionOptionsFactory.Create(requests, client, account, window, cooldown));
}
