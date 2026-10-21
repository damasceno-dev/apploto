using server.Application.Services.Security;

namespace server.Configuration;

internal static class AccountSecurityConfiguration
{
    public static void AddAccountSecurityConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(AccountSecurityOptionsFactory.Create(
            configuration["AccountSecurity:ApplicationUrl"], configuration["AccountSecurity:ResetMinutes"],
            configuration["AccountSecurity:InvitationHours"], configuration["AccountSecurity:RecoveryCooldownSeconds"]));
        services.AddSingleton(LoginProtectionOptionsFactory.Create(
            configuration["AccountSecurity:LoginRequestsPerMinute"], configuration["AccountSecurity:LoginClientFailureLimit"],
            configuration["AccountSecurity:LoginAccountFailureLimit"], configuration["AccountSecurity:LoginFailureWindowMinutes"],
            configuration["AccountSecurity:LoginCooldownMinutes"]));
    }
}
