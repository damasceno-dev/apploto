using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Application.Services.Security;

public sealed class AccountPasswordUpdater(IAccountSecurityRepository security, ILoginThrottleRepository loginThrottles,
    PasswordEncryption passwords)
{
    public async Task Execute(User user, string password)
    {
        user.Password = passwords.HashPassword(password);
        await security.RevokeAllRefreshTokens(user.Id);
        await security.InvalidateRecovery(user.Id);
        // A new password supersedes every guess at the old one, so the account's login budgets start over.
        await loginThrottles.RemoveAccount(LoginAttemptGuard.AccountKey(user.Email));
    }
}
