using server.Application.Services.Security;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.AccountSecurity;

public sealed class AccountSecurityOptionsFactoryTest
{
    [Fact]
    public void ExplicitValues_ShouldApplyTheSameBoundsForApiAndCli()
    {
        var options = AccountSecurityOptionsFactory.Create("https://app.example.invalid/", "7", "24", "90");
        options.ResetLifetime.ShouldBe(TimeSpan.FromMinutes(7));
        options.InvitationLifetime.ShouldBe(TimeSpan.FromHours(24));
        options.RecoveryCooldown.ShouldBe(TimeSpan.FromSeconds(90));
    }

    [Theory]
    [InlineData("0", null, null)]
    [InlineData("61", null, null)]
    [InlineData(null, "169", null)]
    [InlineData(null, null, "3601")]
    public void OutOfBounds_ShouldFailClosed(string? reset, string? invitation, string? cooldown) =>
        Should.Throw<InvalidOperationException>(() => AccountSecurityOptionsFactory.Create(
            "https://app.example.invalid/", reset, invitation, cooldown));

    [Fact]
    public void UntrustedLinkBase_ShouldBeRefused() =>
        Should.Throw<InvalidOperationException>(() => AccountSecurityOptionsFactory.Create(
            "http://app.example.invalid/", null, null, null));
}
